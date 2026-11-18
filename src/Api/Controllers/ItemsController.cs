using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ItemsController(MongoItemStore store) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Item>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await store.GetAllAsync(cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<Item>> Get(string id, CancellationToken cancellationToken)
    {
        var item = await store.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<Item>> Create(Item item, CancellationToken cancellationToken)
    {
        var created = await store.CreateAsync(item, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        Item item,
        CancellationToken cancellationToken) =>
        await store.UpdateAsync(id, item, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        await store.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();
}
