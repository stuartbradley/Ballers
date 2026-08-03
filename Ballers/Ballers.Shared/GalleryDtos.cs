namespace Ballers.Shared
{
    public class PhotographerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Website { get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? Email { get; set; }
        public string? Bio { get; set; }
        public string? LogoUrl { get; set; }
    }

    /// <summary>A gallery as it appears on the galleries index: one cover plus the fixture it belongs to.</summary>
    public class GallerySummaryDto
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public string? Title { get; set; }

        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public bool IsPlayed { get; set; }
        public DateTime? Kickoff { get; set; }
        public int Week { get; set; }

        public string? PhotographerName { get; set; }

        /// <summary>Small cover for dense card grids.</summary>
        public string? CoverThumbnailUrl { get; set; }

        /// <summary>Full-size cover, for the few places a cover is shown large.</summary>
        public string? CoverImageUrl { get; set; }

        public int PhotoCount { get; set; }
        public bool IsPublished { get; set; }
    }

    /// <summary>Tells the fixture page whether this fixture has a gallery to link to.</summary>
    public class FixtureGalleryRefDto
    {
        public int GalleryId { get; set; }
        public int PhotoCount { get; set; }
    }

    /// <summary>The gallery page header: fixture context plus who shot it.</summary>
    public class GalleryDetailDto
    {
        public int Id { get; set; }
        public int FixtureId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public bool IsPlayed { get; set; }
        public DateTime? Kickoff { get; set; }
        public int Week { get; set; }
        public string? Location { get; set; }

        public PhotographerDto? Photographer { get; set; }
        public int PhotoCount { get; set; }
        public bool IsPublished { get; set; }
    }

    public class GalleryPhotoDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = "";
        public string ThumbnailUrl { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int SortOrder { get; set; }

        /// <summary>Height as a percentage of width, so the grid can hold space before the image arrives.</summary>
        public double AspectRatio => Width > 0 ? (double)Height / Width : 1;
    }

    /// <summary>One page of photos. Galleries run to hundreds, so they arrive in chunks.</summary>
    public class GalleryPhotoPageDto
    {
        public List<GalleryPhotoDto> Photos { get; set; } = new();
        public int Total { get; set; }
        public bool HasMore { get; set; }
    }

    public class SaveGalleryRequest
    {
        public int FixtureId { get; set; }
        public int? PhotographerId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public bool IsPublished { get; set; } = true;
    }
}
