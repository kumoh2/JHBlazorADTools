namespace JHBlazorADTools.Models
{
    /// <summary>
    /// AD 사용자 정보를 담는 DTO
    /// </summary>
    public class AdUser
    {
        public string Name { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public DateTime? LockoutTime { get; set; }
    }
}