using JHBlazorADTools.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) AD ���� ���� (����)
var adConfig = new AdConfig
{
    // ������ LDAPS: "mydomain.com:636"
    LdapServer = "jhsoft.org",
    LdapBaseDn = "OU=Users,DC=mydomain,DC=com",
    LdapUsername = "Administrator",
    LdapPassword = "P@ssw0rd123"
};

// 2) DI ���
builder.Services.AddSingleton(adConfig);
builder.Services.AddScoped<AdService>();

// Blazor Server �⺻ ����
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// �⺻ Blazor ����������
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
