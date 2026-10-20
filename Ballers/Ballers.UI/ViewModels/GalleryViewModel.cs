using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// One match's photo gallery: photos load a page at a time as you scroll,
    /// a lightbox steps through them, and links lead to the next and previous
    /// galleries. The page does the browser work (scroll watching, focus,
    /// downloads, sharing) and calls in here for the state.
    /// </summary>
    public class GalleryViewModel : ViewModelBase
    {
        public const int PageSize = 40;

        private readonly GalleryService _galleries;

        public GalleryViewModel(GalleryService galleries) => _galleries = galleries;

        public int Id { get; private set; }
        public GalleryDetailDto? Gallery { get; private set; }
        public List<GalleryPhotoDto> Photos { get; } = new();
        // Newest first.
        public List<GallerySummaryDto> AllGalleries { get; private set; } = new();

        public bool IsLoading { get; private set; } = true;
        public bool IsLoadingMore { get; private set; }
        public bool HasMore { get; private set; } = true;
        public int? LightboxIndex { get; private set; }
        public string? ShareLabel { get; private set; }

        // How long "Link copied" stays up.
        public TimeSpan ShareLabelDuration { get; set; } = TimeSpan.FromMilliseconds(2500);

        // Set when the lightbox opens or moves, for the page to act on once drawn.
        public bool FocusLightboxPending { get; set; }
        public bool CentreThumbPending { get; set; }

        public GallerySummaryDto? Summary => AllGalleries.FirstOrDefault(g => g.Id == Id);

        private int SummaryIndex => AllGalleries.FindIndex(g => g.Id == Id);
        public GallerySummaryDto? Newer => SummaryIndex > 0 ? AllGalleries[SummaryIndex - 1] : null;
        public GallerySummaryDto? Older => SummaryIndex >= 0 && SummaryIndex < AllGalleries.Count - 1 ? AllGalleries[SummaryIndex + 1] : null;

        // The gallery's chosen cover, or its first photo when it has none listed.
        public string? CoverPath =>
            Summary?.CoverImageUrl ?? Summary?.CoverThumbnailUrl ?? Photos.FirstOrDefault()?.ImageUrl;

        public async Task LoadAsync(int id)
        {
            Id = id;
            IsLoading = true;
            Photos.Clear();
            HasMore = true;
            LightboxIndex = null;
            ShareLabel = null;

            var galleryTask = _galleries.GetGallery(Id);
            var listTask = AllGalleries.Any() ? Task.FromResult(AllGalleries) : _galleries.GetGalleries();
            await Task.WhenAll(galleryTask, listTask);

            Gallery = galleryTask.Result;
            AllGalleries = listTask.Result;

            if (Gallery != null) await LoadMoreAsync();

            IsLoading = false;
        }

        public async Task LoadMoreAsync()
        {
            if (!HasMore || IsLoadingMore) return;

            IsLoadingMore = true;
            NotifyChanged();

            var page = await _galleries.GetPhotos(Id, Photos.Count, PageSize);

            Photos.AddRange(page.Photos);
            HasMore = page.HasMore;

            IsLoadingMore = false;
            NotifyChanged();
        }

        // ── Lightbox ──

        public void OpenLightbox(int index)
        {
            LightboxIndex = index;
            FocusLightboxPending = true;
            CentreThumbPending = true;
        }

        public void CloseLightbox() => LightboxIndex = null;

        public void ShowAt(int index)
        {
            if (index < 0 || index >= Photos.Count) return;
            LightboxIndex = index;
            CentreThumbPending = true;
        }

        public void ShowPrevious()
        {
            if (LightboxIndex is > 0) ShowAt(LightboxIndex.Value - 1);
        }

        public async Task ShowNext()
        {
            if (LightboxIndex is not { } current) return;

            // Stepping onto the last loaded photo pulls the next page in, so paging
            // through the lightbox keeps working past the first chunk.
            if (current >= Photos.Count - 2 && HasMore) await LoadMoreAsync();

            if (current < Photos.Count - 1) ShowAt(current + 1);
        }

        /// <summary>The photo open in the lightbox, and the file name to save it as.</summary>
        public (GalleryPhotoDto Photo, string FileName)? CurrentDownload
        {
            get
            {
                if (Gallery == null || LightboxIndex is not { } i || i >= Photos.Count) return null;
                return (Photos[i], $"ballers-mw{Gallery.Week}-{Slug(Gallery.HomeTeam)}-v-{Slug(Gallery.AwayTeam)}-{i + 1}.jpg");
            }
        }

        private static string Slug(string s) =>
            new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

        public string? ShareTitle => Gallery == null
            ? null
            : $"{Gallery.HomeTeam} v {Gallery.AwayTeam} — Match Week {Gallery.Week} photos";

        /// <summary>Shows how sharing went ("copied" or "failed"); a native share
        /// sheet speaks for itself, so anything else shows nothing.</summary>
        public async Task ShowShareResult(string result)
        {
            ShareLabel = result switch
            {
                "copied" => "Link copied",
                "failed" => "Couldn't share",
                _ => null
            };
            if (ShareLabel == null) return;

            NotifyChanged();
            await Delay(ShareLabelDuration);
            ShareLabel = null;
            NotifyChanged();
        }
    }
}
