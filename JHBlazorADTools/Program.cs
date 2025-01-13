using JHBlazorADTools.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) AD 설정 정보 (예시)
var adConfig = new AdConfig
{
    // 가능한 LDAPS: "mydomain.com:636"
    LdapServer = "jhsoft.org",
    LdapBaseDn = "OU=Users,DC=mydomain,DC=com",
    LdapUsername = "Administrator",
    LdapPassword = "P@ssw0rd123"
};

// 2) DI 등록
builder.Services.AddSingleton(adConfig);
builder.Services.AddScoped<AdService>();

// Blazor Server 기본 설정
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// 기본 Blazor 파이프라인
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
