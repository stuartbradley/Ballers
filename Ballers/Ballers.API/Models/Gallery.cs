namespace Ballers.API.Models
{
    /// <summary>
    /// The person who shot a gallery. Held separately from the gallery so the
    /// same photographer can be credited week after week, and correcting a link
    /// updates every gallery at once.
    /// </summary>
    public class Photographer
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

    /// <summary>A set of match photos attached to one fixture.</summary>
    public class Gallery
    {
        public int Id { get; set; }

        public int FixtureId { get; set; }
        public Fixture? Fixture { get; set; }

        public int? PhotographerId { get; set; }
        public Photographer? Photographer { get; set; }

        public string? Title { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Lets a gallery be prepared before it appears on the site.</summary>
        public bool IsPublished { get; set; } = true;

        public ICollection<GalleryPhoto> Photos { get; set; } = new List<GalleryPhoto>();
    }

    public class GalleryPhoto
    {
        public int Id { get; set; }

        public int GalleryId { get; set; }
        public Gallery? Gallery { get; set; }

        /// <summary>
        /// The name the photographer's file arrived with. Kept because it carries
        /// the shooting order, which is the order the match actually happened in.
        /// The stored file gets a unique suffix, so this is the only record of it.
        /// </summary>
        public string OriginalFileName { get; set; } = "";

        /// <summary>Full size image, served from wwwroot/uploads/galleries.</summary>
        public string ImageUrl { get; set; } = "";

        /// <summary>Smaller copy used for the grids, so a page load is a few MB not tens.</summary>
        public string ThumbnailUrl { get; set; } = "";

        /// <summary>
        /// Stored so the grid can reserve the right space before an image loads,
        /// which keeps a lazily loaded mosaic from jumping around as you scroll.
        /// </summary>
        public int Width { get; set; }
        public int Height { get; set; }

        public int SortOrder { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
