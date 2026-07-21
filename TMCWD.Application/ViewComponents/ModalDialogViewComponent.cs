using Microsoft.AspNetCore.Mvc;

namespace TMCWD.Application.ViewComponents
{
    public class ModalDialogViewComponent : ViewComponent
    {
        /// <summary>
        /// Show modal component in view
        /// </summary>
        /// <param name="type">Type of dialog to add. Multiple values are accepted provided with space separator. Allowed values are deactivate, success, number, and all</param>
        /// <returns></returns>
        public async Task<IViewComponentResult> InvokeAsync(string type = "deactivate success number")
        {
            return View("Default", type);
        }

    }
}
