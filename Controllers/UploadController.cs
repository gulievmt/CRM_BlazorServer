using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace CRMBlazorServerRBS.Controllers
{
    public partial class UploadController : Controller
    {
        private readonly IWebHostEnvironment environment;

        public UploadController(IWebHostEnvironment environment)
        {
            this.environment = environment;
        }

        // Single file upload
        [HttpPost("upload/single")]
        public IActionResult Single(IFormFile file)
        {
            try
            {
                // Put your code here
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Multiple files upload
        [HttpPost("upload/multiple")]
        public IActionResult Multiple(IFormFile[] files)
        {
            try
            {
                // Put your code here
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Multiple files upload with parameter
        [HttpPost("upload/{id}")]
        public IActionResult Post(IFormFile[] files, int id)
        {
            try
            {
                // Put your code here
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // Image file upload (used by HtmlEditor components)
        [HttpPost("upload/image")]
        public IActionResult Image(IFormFile file)
        {
            try
            {
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                using (var stream = new FileStream(Path.Combine(environment.WebRootPath, fileName), FileMode.Create))
                {
                    // Save the file
                    file.CopyTo(stream);

                    // Return the URL of the file
                    var url = Url.Content($"~/{fileName}");

                    return Ok(new { Url = url });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Large file upload endpoint — bypasses Blazor SignalR 10 MB message-size limit.
        /// The browser sends multipart/form-data directly to this HTTP endpoint via XHR,
        /// so the file never passes through the SignalR connection.
        /// Kestrel limit is disabled via [DisableRequestSizeLimit]; configure the maximum
        /// acceptable size in appsettings.json → FileUpload:MaxFileSizeBytes.
        /// </summary>
        [HttpPost("upload/large")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> Large(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("Файл не выбран или пуст.");

                // Save files to wwwroot/uploads/
                var uploadsFolder = Path.Combine(environment.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder);

                // Build a safe, unique file name: originalName_<guid>.ext
                var nameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName)
                    .Replace(" ", "_");
                var ext = Path.GetExtension(file.FileName);
                var uniqueName = $"{nameWithoutExt}_{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadsFolder, uniqueName);

                // Stream directly to disk — no intermediate byte[] in memory
                await using var fileStream = new FileStream(
                    fullPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, bufferSize: 81_920, useAsync: true);
                await file.CopyToAsync(fileStream);

                var url = Url.Content($"~/uploads/{uniqueName}");

                return Ok(new
                {
                    Url = url,
                    FileName = file.FileName,
                    SavedAs = uniqueName,
                    Size = file.Length
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
