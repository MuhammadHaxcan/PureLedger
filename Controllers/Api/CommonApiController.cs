using Microsoft.AspNetCore.Mvc;
;

[ApiController]
[Route("api/common")]
public class CommonApiController : ControllerBase
{
    private readonly ICommonApiService _commonApiService;

    public CommonApiController(ICommonApiService commonApiService)
    {
        _commonApiService = commonApiService;
    }

    [HttpGet("projects")]
    public async Task<IActionResult> GetProjects()
    {
        var projects = await _commonApiService.GetProjectsAsync();
        var result = projects.Select(p => new { id = p.ProjectId, name = p.ProjectName });
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategoriesByProject([FromQuery] int projectId)
    {
        var categories = await _commonApiService.GetCategoriesByProjectAsync(projectId);
        var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
        return Ok(result);
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccountsByCategory([FromQuery] int categoryId)
    {
        var accounts = await _commonApiService.GetAccountsByCategoryAsync(categoryId);
        var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
        return Ok(result);
    }
}