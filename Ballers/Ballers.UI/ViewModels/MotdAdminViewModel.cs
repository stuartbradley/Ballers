using Ballers.UI.Infrastructure;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for Match of the Day posts: write one for a fixture with a cover
    /// photo and a gallery, edit or delete existing ones. The page confirms a
    /// delete before calling <see cref="Delete"/>.
    /// </summary>
    public class MotdAdminViewModel : ViewModelBase
    {
        public const long MaxPhotoBytes = 8 * 1024 * 1024;

        private readonly MotdService _motd;

        public MotdAdminViewModel(MotdService motd) => _motd = motd;

        public List<MotdListItemDto> Posts { get; private set; } = new();
        public List<MotdFixtureOptionDto> Options { get; private set; } = new();
        public bool IsLoading { get; private set; } = true;

        public int? EditingId { get; private set; }
        public int SelectedFixtureId { get; set; }
        public string? CoverBase64 { get; private set; }
        public string Body { get; set; } = "";
        public List<string> GalleryPhotos { get; private set; } = new();

        public bool Saving { get; private set; }
        public bool SavedOk { get; private set; }
        public string? Error { get; private set; }

        public async Task InitializeAsync()
        {
            Options = await _motd.GetFixtureOptions();
            await LoadPosts();
        }

        private async Task LoadPosts()
        {
            IsLoading = true;
            Posts = await _motd.GetList();
            IsLoading = false;
        }

        public async Task SetCover(IBrowserFile file)
        {
            Error = null;
            var dataUrl = await ToDataUrl(file);
            if (dataUrl != null) CoverBase64 = dataUrl;
        }

        public async Task AddGalleryPhotos(IEnumerable<IBrowserFile> files)
        {
            Error = null;
            foreach (var file in files)
            {
                var dataUrl = await ToDataUrl(file);
                if (dataUrl != null) GalleryPhotos.Add(dataUrl);
            }
        }

        private async Task<string?> ToDataUrl(IBrowserFile file)
        {
            if (file.Size > MaxPhotoBytes)
            {
                Error = $"'{file.Name}' is over 8 MB and was skipped.";
                return null;
            }
            await using var stream = file.OpenReadStream(maxAllowedSize: MaxPhotoBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return $"data:{file.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        public void RemovePhoto(int index) => GalleryPhotos.RemoveAt(index);

        public async Task Save()
        {
            Error = null;
            SavedOk = false;

            if (SelectedFixtureId <= 0) { Error = "Select a match."; return; }
            if (string.IsNullOrEmpty(CoverBase64)) { Error = "A cover photo is required."; return; }

            Saving = true;
            var request = new SaveMotdRequest
            {
                FixtureId = SelectedFixtureId,
                CoverImageBase64 = CoverBase64,
                Body = Body,
                Photos = GalleryPhotos
            };

            try
            {
                await (EditingId == null ? _motd.Create(request) : _motd.Update(EditingId.Value, request));
            }
            catch (ApiException ex)
            {
                Error = ex.Message;
                return;
            }
            finally
            {
                Saving = false;
            }

            SavedOk = true;
            ResetForm();
            await LoadPosts();
        }

        public async Task StartEdit(int id)
        {
            var detail = await _motd.GetById(id);
            if (detail == null) return;

            EditingId = detail.Id;
            SelectedFixtureId = detail.FixtureId;
            CoverBase64 = detail.CoverImageBase64;
            Body = detail.Body;
            GalleryPhotos = new List<string>(detail.Photos);
            Error = null;
            SavedOk = false;
        }

        public async Task Delete(int id)
        {
            try { await _motd.Delete(id); }
            catch (ApiException ex) { Error = ex.Message; return; }
            if (EditingId == id) ResetForm();
            await LoadPosts();
        }

        public void ResetForm()
        {
            EditingId = null;
            SelectedFixtureId = 0;
            CoverBase64 = null;
            Body = "";
            GalleryPhotos = new();
        }
    }
}
