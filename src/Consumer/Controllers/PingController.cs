using Microsoft.AspNetCore.Mvc;

namespace Consumer.Controllers;

[ApiController]
[Route("[controller]")]
public class PingController : ControllerBase
{
    private readonly ILogger<PingController> _logger;

    public PingController(
        ILogger<PingController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route("/ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }
}
