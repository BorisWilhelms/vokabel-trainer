namespace VokabelTrainer.Api.Controllers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VokabelTrainer.Api.Services;
using VokabelTrainer.Api.Models.Training;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrainingController(TrainingService trainingService) : ControllerBase
{
    [HttpPost("start")]
    public async Task<ActionResult<int>> Start(StartSessionRequest request)
    {
        var sessionId = await trainingService.StartSessionAsync(
            GetUserId(), request.ListId, request.Mode, request.MaxVocabulary);
        return Ok(sessionId);
    }

    [HttpGet("next-question/{sessionId}")]
    public async Task<ActionResult<TrainingQuestionDto>> GetNextQuestion(int sessionId)
    {
        var question = await trainingService.GetNextQuestionAsync(sessionId);
        if (question is null)
            return NoContent();
        return Ok(question);
    }

    [HttpPost("submit-answer")]
    public async Task<ActionResult<SubmitAnswerResponse>> SubmitAnswer(SubmitAnswerRequest request)
    {
        var result = await trainingService.SubmitAnswerAsync(
            request.SessionId, request.VocabularyId, request.Direction, request.Answer);
        return Ok(result);
    }

    [HttpPost("abort/{sessionId}")]
    public async Task<IActionResult> Abort(int sessionId)
    {
        await trainingService.AbortSessionAsync(sessionId);
        return Ok();
    }

    [HttpGet("result/{sessionId}")]
    public async Task<ActionResult<SessionResultDto>> GetResult(int sessionId)
    {
        var result = await trainingService.GetSessionResultAsync(sessionId);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    private int GetUserId() =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException());
}
