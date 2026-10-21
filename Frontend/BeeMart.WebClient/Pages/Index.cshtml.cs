using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BeeMart.WebClient.Pages;

[Authorize]
public class IndexModel : PageModel
{
    // tidak ada landing page, langsung ke New Transaction
    public IActionResult OnGet() => RedirectToPage("/NewTransaction");
}
