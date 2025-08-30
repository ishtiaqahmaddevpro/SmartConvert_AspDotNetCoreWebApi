using Microsoft.AspNetCore.Mvc;
using Xabe.FFmpeg;

namespace SmartConvert.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConvertController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // Inject IConfiguration into the controller's constructor
        public ConvertController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpPost("video-to-audio")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> ConvertVideoToAudio([FromForm] FileUploadDto fileUpload)
        {
            if (fileUpload.VideoFile == null)
                return BadRequest("No file provided.");

            try
            {
                var videoFile = fileUpload.VideoFile;

                // Save the uploaded video to a temporary location
                // Path.GetTempPath() is fine as it's for temporary work files
                var tempVideoPath = Path.Combine(Path.GetTempPath(), videoFile.FileName);
                using (var stream = new FileStream(tempVideoPath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(stream);
                }

                // Define output audio file path
                var outputAudioPath = Path.ChangeExtension(tempVideoPath, ".mp3");

                // Get FFmpeg path from configuration
                var ffmpegPath = _configuration.GetValue<string>("FFmpegSettings:ExecutablesPath");

                // Check if path is configured. If not, fallback to default or throw an error.
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    // Fallback to AppContext.BaseDirectory/ffmpeg/bin if not configured,
                    // or throw an exception if you want to force configuration.
                    ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin");
                    // Or simply throw new InvalidOperationException("FFmpeg executable path not configured.");
                }

                // Set FFmpeg executables path
                // Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegPath); // Assuming Xabe.FFmpeg for this line

                // It's generally better to set Xabe.FFmpeg's path once at application startup (e.g., in Program.cs)
                // rather than on every request, especially if it's a static method.
                // However, for demonstration, we'll keep it here, but ideally move it.
                FFmpeg.SetExecutablesPath(ffmpegPath);


                // Convert video to audio
                var conversion = await FFmpeg.Conversions.FromSnippet.ExtractAudio(tempVideoPath, outputAudioPath);
                await conversion.Start();

                // Return the audio file to the user
                var audioBytes = await System.IO.File.ReadAllBytesAsync(outputAudioPath);
                var fileName = Path.GetFileName(outputAudioPath);

                // Clean up temporary files
                System.IO.File.Delete(tempVideoPath);
                System.IO.File.Delete(outputAudioPath);

                return File(audioBytes, "audio/mpeg", fileName);
            }
            catch (Exception ex)
            {
                // Log the exception details properly in a real application
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}