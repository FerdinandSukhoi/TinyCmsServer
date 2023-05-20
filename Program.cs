using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Steeltoe.Extensions.Configuration.Placeholder;
using static Microsoft.AspNetCore.Http.Results;
using File = System.IO.File;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddPlaceholderResolver();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();


var contentDir = Path.GetFullPath(app.Configuration["ResourceDir"] ?? "/contents");
bool CheckPath(string path) => path.StartsWith(contentDir + Path.DirectorySeparatorChar) && !Directory.Exists(path);
string ContentPath(string subPath) => Path.Combine(contentDir!, subPath.Replace("%2F", "/"));
void EnsureDirectory(string path) => Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
app.UseFileServer(new FileServerOptions
{
    EnableDirectoryBrowsing = true,
    FileProvider = new PhysicalFileProvider(contentDir),
    RequestPath = "/Files",
    StaticFileOptions =
    {
        ContentTypeProvider = new FileExtensionContentTypeProvider(),
        ServeUnknownFileTypes = true
    }
});



app.MapGet("/Exists/{**path}",
    (string path) =>
    {
        path = ContentPath(path);
        return !CheckPath(path) ? BadRequest() : Ok(File.Exists(path));
    });
if (app.Configuration.GetValue("AllowDelete", false))
    app.MapDelete("/Delete/{**path}",
    (string path) =>
    {
        path = ContentPath(path);
        if (!CheckPath(path)) return BadRequest();
        File.Delete(path);
        return NoContent();
    });
if (app.Configuration.GetValue("AllowDeleteDir", false))
    app.MapDelete("/DeleteDir/{**path}",
    (string path) =>
    {
        path = ContentPath(path);
        if (!path.StartsWith(contentDir + Path.DirectorySeparatorChar)) return BadRequest();
        Directory.Delete(path);
        return NoContent();
    });
if (app.Configuration.GetValue("AllowWrite", false))
    app.MapPut("/Upload/{**path}",
    async ([FromRoute] string path, [FromForm] IFormFile file) =>
    {
        path = ContentPath(path);
        if (!CheckPath(path)) return BadRequest();
        Path.GetDirectoryName(path);
        EnsureDirectory(path);
        if (File.Exists(path)) return BadRequest();
        await using var rs = file.OpenReadStream();
        await using var fs = File.OpenWrite(path);
        await rs.CopyToAsync(fs);
        fs.Close();
        return NoContent();
    });
if (app.Configuration.GetValue("AllowOverWrite", false))
    app.MapPut("/UploadOrUpdate/{**path}",
    async ([FromRoute] string path, [FromForm] IFormFile file) =>
    {
        path = ContentPath(path);
        if (!CheckPath(path)) return BadRequest();
        EnsureDirectory(path);
        await using var rs = file.OpenReadStream();
        await using var fs = File.OpenWrite(path);
        await rs.CopyToAsync(fs);
        fs.Close();
        return NoContent();
    });

app.Run();
