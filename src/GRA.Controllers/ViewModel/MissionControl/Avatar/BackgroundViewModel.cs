using Microsoft.AspNetCore.Http;

namespace GRA.Controllers.ViewModel.MissionControl.Avatar
{
    public class BackgroundViewModel
    {
        public bool BackgroundExists { get; set; }
        public string BackgroundImageUrl { get; set; }
        public IFormFile UploadedFile { get; set; }
    }
}
