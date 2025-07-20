using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PureLedger.Services;

namespace PureLedger.Controllers.Api
{
    [Route("api/common")]
    [ApiController]
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

        [HttpGet("categories/{projectId}")]
        public async Task<IActionResult> GetCategoriesByProject([FromRoute] int projectId)
        {
            var categories = await _commonApiService.GetCategoriesByProjectAsync(projectId);
            var result = categories.Select(c => new { id = c.CategoryId, name = c.CategoryName });
            return Ok(result);
        }

        [HttpGet("accounts/{categoryId}")]
        public async Task<IActionResult> GetAccountsByCategory([FromRoute] int categoryId)
        {
            var accounts = await _commonApiService.GetAccountsByCategoryAsync(categoryId);
            var result = accounts.Select(a => new { id = a.AccountId, name = a.AccountName });
            return Ok(result);
        }
    }
}
