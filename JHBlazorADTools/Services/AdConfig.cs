namespace JHBlazorADTools.Services
{
    /// <summary>
    /// AD 접속에 필요한 설정 정보
    /// </summary>
    public class AdConfig
    {
        /// <summary>
        /// 예) "mydomain.com:636" (LDAPS 포트 권장)
        /// </summary>
        public string LdapServer { get; set; } = string.Empty;

        /// <summary>
        /// 검색 범위가 될 Base DN (예: "OU=Users,DC=mydomain,DC=com")
        /// </summary>
        public string LdapBaseDn { get; set; } = string.Empty;

        /// <summary>
        /// AD(또는 LDAP) 접속 계정
        /// </summary>
        public string LdapUsername { get; set; } = string.Empty;

        /// <summary>
        /// AD(또는 LDAP) 접속 비밀번호
        /// </summary>
        public string LdapPassword { get; set; } = string.Empty;
    }
}