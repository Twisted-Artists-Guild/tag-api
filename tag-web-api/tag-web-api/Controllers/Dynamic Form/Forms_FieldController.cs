// <copyright file="Forms_FieldController.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using TAGWEBAPI.Data;
    using TAGWEBAPI.Models;

    [Route("api/[controller]")]
    [ApiController]
    public class Forms_FieldController : ControllerBase
    {
        private readonly TAGDBContext context;

        public Forms_FieldController(TAGDBContext context)
        {
            this.context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Forms_Field>>> Get()
        {
            return await this.context.Set<Forms_Field>().ToListAsync().ConfigureAwait(false);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Forms_Field>> GetForms_Field(int id)
        {
            var forms_field = await this.context.Set<Forms_Field>().FindAsync(id).ConfigureAwait(false);
            if (forms_field == null)
            {
                return this.NotFound();
            }

            return forms_field;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Forms_Field>> Create(Forms_Field forms_field)
        {
            this.context.Set<Forms_Field>().Add(forms_field);
            await this.context.SaveChangesAsync().ConfigureAwait(false);
            return this.CreatedAtAction(nameof(this.Get), new { id = forms_field.Forms_FieldID }, forms_field);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, Forms_Field forms_field)
        {
            if (id != forms_field.Forms_FieldID)
            {
                return this.BadRequest();
            }

            this.context.Entry(forms_field).State = EntityState.Modified;

            try
            {
                await this.context.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!this.Forms_FieldExists(id))
                {
                    return this.NotFound();
                }
                else
                {
                    throw;
                }
            }

            return this.NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var forms_field = await this.context.Set<Forms_Field>().FindAsync(id).ConfigureAwait(false);
            if (forms_field == null)
            {
                return this.NotFound();
            }

            this.context.Set<Forms_Field>().Remove(forms_field);
            await this.context.SaveChangesAsync().ConfigureAwait(false);

            return this.NoContent();
        }

        private bool Forms_FieldExists(int id)
        {
            return this.context.Set<Forms_Field>().Any(e => e.Forms_FieldID == id);
        }
    }
}
