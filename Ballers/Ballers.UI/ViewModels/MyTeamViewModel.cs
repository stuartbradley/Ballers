using Ballers.UI.Infrastructure;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A manager's club profile: name, kit, bio, the crest and the manager's photo.
    /// </summary>
    public class MyTeamViewModel : ViewModelBase
    {
        public const long MaxImageBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };

        private readonly TeamService _teams;
        private readonly AuthenticationStateProvider _auth;

        public MyTeamViewModel(TeamService teams, AuthenticationStateProvider auth)
        {
            _teams = teams;
            _auth = auth;
        }

        public UpdateTeamProfileRequest Form { get; } = new();
        public int? TeamId { get; private set; }
        public string? CurrentImageUrl { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public bool IsSaving { get; private set; }
        public bool IsUploadingImage { get; private set; }
        public string? SaveError { get; private set; }
        public string? ImageError { get; private set; }
        public bool SaveSuccess { get; private set; }
        public bool ImageSuccess { get; private set; }
        public string? ManagerImageBase64 { get; private set; }
        public int ManagerWins { get; private set; }
        public bool ShowManagerCrop { get; set; }
        public bool ManagerImageSuccess { get; set; }

        public async Task InitializeAsync()
        {
            var authState = await _auth.GetAuthenticationStateAsync();
            var teamIdClaim = authState.User.FindFirst("TeamId")?.Value;
            if (int.TryParse(teamIdClaim, out var tid))
                TeamId = tid;

            if (TeamId.HasValue)
            {
                var profile = await _teams.GetProfile(TeamId.Value);
                if (profile != null)
                {
                    Form.Name = profile.Name;
                    Form.ManagerName = profile.ManagerName;
                    Form.YearFormed = profile.YearFormed;
                    Form.PhoneNumber = profile.PhoneNumber;
                    Form.Bio = profile.Bio;
                    Form.HomeKitColour = profile.HomeKitColour;
                    Form.AwayKitColour = profile.AwayKitColour;
                    CurrentImageUrl = profile.ProfileImageUrl;
                    ManagerImageBase64 = profile.ManagerImageBase64;
                    ManagerWins = profile.Wins;
                }
            }

            IsLoading = false;
        }

        public async Task Save()
        {
            SaveError = null;
            SaveSuccess = false;

            if (string.IsNullOrWhiteSpace(Form.Name))
            {
                SaveError = "Team name is required.";
                return;
            }

            IsSaving = true;
            try
            {
                await _teams.UpdateProfile(TeamId!.Value, Form);
                SaveSuccess = true;
            }
            catch (ApiException ex)
            {
                SaveError = ex.Message;
            }
            finally
            {
                IsSaving = false;
            }
        }

        public async Task UploadCrest(IBrowserFile file)
        {
            ImageError = null;
            ImageSuccess = false;

            if (file.Size > MaxImageBytes)
            {
                ImageError = "Image must be under 5 MB.";
                return;
            }

            if (!AllowedImageTypes.Contains(file.ContentType))
            {
                ImageError = "Only JPEG, PNG, and WebP are accepted.";
                return;
            }

            IsUploadingImage = true;
            try
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: MaxImageBytes);
                CurrentImageUrl = await _teams.UploadImage(TeamId!.Value, stream, file.Name, file.ContentType);
                ImageSuccess = true;
            }
            catch (ApiException ex)
            {
                ImageError = ex.Message;
            }
            finally
            {
                IsUploadingImage = false;
            }
        }

        // Called by ImageCropModal with the processed base64 (cropped + background removed).
        public async Task<bool> SaveManagerImage(string base64)
        {
            ManagerImageSuccess = false;
            try
            {
                await _teams.UploadManagerImage(TeamId!.Value, base64);
            }
            catch (ApiException)
            {
                // The crop modal shows its own failure message.
                return false;
            }
            ManagerImageBase64 = base64;
            return true;
        }
    }
}
