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
                    // lockoutTime을 string 으로 꺼냄
                    string lockoutTimeStr = entry.Attributes["lockoutTime"][0]?.ToString();

                    // 문자열을 long으로 변환 시도
                    if (long.TryParse(lockoutTimeStr, out long fileTime) && fileTime > 0)
                    {
                        // FileTime(1601년 1월 1일부터 경과한 100ns 단위)을 DateTime으로 변환
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

        public List<AdUser> GetAllUsers()
        {
            var allUsers = new List<AdUser>();

            using var connection = CreateConnection();
            connection.Bind();

            // 잠금 여부 상관없이 모든 user 검색
            var request = new SearchRequest(
                _config.LdapBaseDn,
                "(&(objectCategory=person)(objectClass=user))",
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
                    // 1) lockoutTime을 문자열로 가져오기
                    string lockoutTimeStr = entry.Attributes["lockoutTime"][0]?.ToString();

                    // 2) long으로 파싱
                    if (long.TryParse(lockoutTimeStr, out long fileTime) && fileTime > 0)
                    {
                        // 3) 0보다 큰 값이면 잠금 시간으로 해석
                        lockoutTime = DateTime.FromFileTimeUtc(fileTime);
                    }
                }

                allUsers.Add(new AdUser
                {
                    Name = name,
                    SamAccountName = sam,
                    LockoutTime = lockoutTime
                });
            }

            return allUsers;
        }

        public bool LockUser(string samAccountName)
        {
            if (string.IsNullOrWhiteSpace(samAccountName))
                return false;

            using var connection = CreateConnection(); // LdapConnection 생성 후 Bind()
            connection.Bind();

            // 1) 사용자 DN 검색
            var searchRequest = new SearchRequest(
                _config.LdapBaseDn,
                $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={samAccountName}))",
                SearchScope.Subtree,
                new[] { "distinguishedName" }
            );

            var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);
            if (searchResponse.Entries.Count == 0)
                return false;

            var userDn = searchResponse.Entries[0].DistinguishedName;

            // 2) lockoutTime 값으로 쓸 FileTime(byte[]) 생성
            long nowFileTime = DateTime.UtcNow.ToFileTimeUtc();
            var fileTimeBytes = BitConverter.GetBytes(nowFileTime);
            // 필요 시 엔디안 확인 (대부분 이대로 OK)

            // 3) DirectoryAttributeModification 객체 생성
            var mod = new DirectoryAttributeModification
            {
                Name = "lockoutTime",
                Operation = DirectoryAttributeOperation.Replace
            };
            // 값 추가 (여러 값 가능하지만, 여기선 1개)
            mod.Add(fileTimeBytes);

            // 4) ModifyRequest에 'mod'를 배열로 담아 전달
            var modifyRequest = new ModifyRequest(userDn, mod);

            // 5) 요청 실행
            var modifyResponse = (ModifyResponse)connection.SendRequest(modifyRequest);

            return modifyResponse.ResultCode == ResultCode.Success;
        }
    }
}
