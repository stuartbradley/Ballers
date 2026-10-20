using Ballers.UI.Infrastructure;
using Ballers.Models;
using Ballers.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The league's referees and their contact details; admins add, edit and
    /// remove them.
    /// </summary>
    public class RefereesViewModel : ViewModelBase
    {
        private readonly RefereeService _referees;
        private readonly AuthenticationStateProvider _auth;

        public RefereesViewModel(RefereeService referees, AuthenticationStateProvider auth)
        {
            _referees = referees;
            _auth = auth;
        }

        public List<RefereeDto> Referees { get; private set; } = new();
        public SaveRefereeRequest Form { get; private set; } = new();
        public int? EditingId { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public bool IsSaving { get; private set; }
        public bool IsAdmin { get; private set; }
        public string? FormError { get; private set; }

        public async Task InitializeAsync()
        {
            var auth = await _auth.GetAuthenticationStateAsync();
            IsAdmin = auth.User.IsInRole("Admin");
            Referees = await _referees.GetAll();
            IsLoading = false;
        }

        public void StartEdit(RefereeDto referee)
        {
            EditingId = referee.Id;
            Form = new SaveRefereeRequest
            {
                Name        = referee.Name,
                PhoneNumber = referee.PhoneNumber,
                Email       = referee.Email,
                Notes       = referee.Notes
            };
            FormError = null;
        }

        public void CancelEdit()
        {
            EditingId = null;
            Form = new SaveRefereeRequest();
            FormError = null;
        }

        public async Task Save()
        {
            FormError = null;

            if (string.IsNullOrWhiteSpace(Form.Name))
            {
                FormError = "Name is required.";
                return;
            }

            IsSaving = true;

            try
            {
                if (EditingId.HasValue)
                {
                    var updated = await _referees.Update(EditingId.Value, Form);
                    var idx = Referees.FindIndex(r => r.Id == EditingId.Value);
                    if (idx >= 0) Referees[idx] = updated;
                    CancelEdit();
                }
                else
                {
                    Referees.Add(await _referees.Create(Form));
                    Referees = Referees.OrderBy(r => r.Name).ToList();
                    Form = new SaveRefereeRequest();
                }
            }
            catch (ApiException ex)
            {
                FormError = ex.Message;
            }
            finally
            {
                IsSaving = false;
            }
        }

        public async Task Delete(int id)
        {
            try { await _referees.Delete(id); }
            catch (ApiException ex) { FormError = ex.Message; return; }
            Referees.RemoveAll(r => r.Id == id);
        }
    }
}
