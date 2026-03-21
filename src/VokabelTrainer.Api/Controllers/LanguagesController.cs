namespace VokabelTrainer.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Shared.Dtos.Languages;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LanguagesController(LanguageService languageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LanguageDto>>> GetAll()
    {
        return Ok(await languageService.GetAllAsync());
    }

    [HttpPost]
    [Authorize("AdminOnly")]
    public async Task<ActionResult<LanguageDto>> Create(CreateLanguageRequest request)
    {
        var result = await languageService.CreateAsync(request);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize("AdminOnly")]
    public async Task<ActionResult<LanguageDto>> Update(int id, UpdateLanguageRequest request)
    {
        var result = await languageService.UpdateAsync(id, request);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize("AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await languageService.DeleteAsync(id);
        if (!success)
            return NotFound();
        return Ok();
    }
}
