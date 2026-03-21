namespace VokabelTrainer.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Shared.Dtos.Users;

[ApiController]
[Route("api/[controller]")]
[Authorize("AdminOnly")]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        return Ok(await userService.GetAllAsync());
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
    {
        var result = await userService.CreateAsync(request);
        return Ok(result);
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var success = await userService.ResetPasswordAsync(id);
        if (!success)
            return NotFound();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await userService.DeleteAsync(id);
        if (!success)
            return NotFound();
        return Ok();
    }
}
