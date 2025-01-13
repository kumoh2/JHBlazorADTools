using System.DirectoryServices.Protocols;
using System.Net;
using JHBlazorADTools.Models;

namespace JHBlazorADTools.Services
{
    public class AdService
    {
        private readonly AdConfig _config;

        public AdService(AdConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 잠금된 사용자 조회 (lockoutTime >= 1)
        /// </summary>
        public List<AdUser> GetLockedOutUsers()
        {
            var lockedUsers = new List<AdUser>();

            using var connection = CreateConnection();
            connection.Bind();  // LDAP 인증

            // LDAP Filter: 잠금된 계정 검색
            // (&(objectCategory=person)(objectClass=user)(lockoutTime>=1))
            var request = new SearchRequest(
                _config.LdapBaseDn,
                "(&(objectCategory=person)(objectClass=user)(lockoutTime>=1))",
                SearchScope.Subtree,
                new[] { "name", "samAccountName", "lockoutTime" }
            );

            var response = (SearchResponse)connection.SendRequest(request);

            foreach (SearchResultEntry entry in response.Entries)
            {
                var name = entry.Attributes["name"]?[0]?.ToString() ?? string.Empty;
                var sam = entry.Attributes["samAccountName"]?[0]?.ToString() ?? string.Empty;

                DateTime? lockoutTime = null;
                if (entry.Attributes["lockoutTime"]?.Count > 0)
                {
                    // AD lockoutTime은 1601년 1월 1일부터의 FileTime (100ns 단위)
                    long fileTime = (long)entry.Attributes["lockoutTime"][0];
                    if (fileTime > 0)
                    {
                        lockoutTime = DateTime.FromFileTimeUtc(fileTime);
                    }
                }

                lockedUsers.Add(new AdUser
                {
                    Name = name,
                    SamAccountName = sam,
                    LockoutTime = lockoutTime
                });
            }

            return lockedUsers;
        }

        /// <summary>
        /// 특정 사용자 계정의 잠금 해제
        /// </summary>
        public bool UnlockUser(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
                return false;

            using var connection = CreateConnection();
            connection.Bind();

            // 해당 사용자 DN(distinguishedName) 검색
            var searchRequest = new SearchRequest(
                _config.LdapBaseDn,
                $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={samAccountName}))",
                SearchScope.Subtree,
                new[] { "distinguishedName" }
            );

            var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);
            if (searchResponse.Entries.Count == 0)
            {
                return false; // 해당 사용자 없음
            }

            var userDn = searchResponse.Entries[0].DistinguishedName;

            // lockoutTime = 0으로 업데이트 -> 계정 잠금 해제
            var modifyRequest = new ModifyRequest(
                userDn,
                DirectoryAttributeOperation.Replace,
                "lockoutTime",
                "0"
            );

            var modifyResponse = (ModifyResponse)connection.SendRequest(modifyRequest);

            return modifyResponse.ResultCode == ResultCode.Success;
        }

        /// <summary>
        /// LdapConnection 생성 헬퍼
        /// </summary>
        private LdapConnection CreateConnection()
        {
            // LDAP 서버 식별자 (ex: "mydomain.com:636" for LDAPS)
            var identifier = new LdapDirectoryIdentifier(_config.LdapServer);
            var connection = new LdapConnection(identifier)
            {
                AuthType = AuthType.Negotiate,
                Credential = new NetworkCredential(_config.LdapUsername, _config.LdapPassword)
            };

            // LDAPS를 사용할 경우:
            // connection.SessionOptions.SecureSocketLayer = true;
            // connection.SessionOptions.VerifyServerCertificate += (conn, cert) => true; // 필요 시 검증 로직

            return connection;
        }
    }
}
