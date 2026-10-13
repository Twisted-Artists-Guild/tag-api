using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MotionsController : ControllerBase
    {
        private readonly TAGDBContext _context;

        public MotionsController(TAGDBContext context)
        {
            _context = context;
        }

        // Helper Method: Parse string like "Weekly" into actual days to add
        private int ParseDurationInDays(string durationString)
        {
            if (string.IsNullOrWhiteSpace(durationString)) return 7; // Default fallback to 1 week
            
            string lowerDuration = durationString.ToLower().Trim();

            if (lowerDuration.Contains("week")) return 7;
            if (lowerDuration.Contains("month")) return 30; // Approx 1 Month
            if (lowerDuration.Contains("quarter")) return 90; // Approx 3 Months
            if (lowerDuration.Contains("year")) return 365; // 1 Year

            return 7; // Fallback
        }

        // Helper Method: Safely expire motions past their deadline
        private async Task CheckExpiration(Motion motion)
        {
            if (motion.Status == "Seconded" && motion.SecondedOn.HasValue)
            {
                int daysAllowed = ParseDurationInDays(motion.Duration);
                DateTime expirationDate = motion.SecondedOn.Value.AddDays(daysAllowed);

                if (DateTime.UtcNow >= expirationDate)
                {
                    motion.Status = "Closed";
                    await _context.SaveChangesAsync();
                }
            }
        }

        // GET: api/Motions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Motion>>> GetMotions()
        {
            var motions = await _context.Motions
                .Include(m => m.ProposedBy)
                .Include(m => m.SecondedBy)
                .OrderByDescending(m => m.ProposedOn)
                .ToListAsync();

            // Expire naturally returned motions instantly
            bool changesMade = false;
            foreach(var motion in motions)
            {
                if (motion.Status == "Seconded" && motion.SecondedOn.HasValue)
                {
                    int daysAllowed = ParseDurationInDays(motion.Duration);
                    if (DateTime.UtcNow >= motion.SecondedOn.Value.AddDays(daysAllowed))
                    {
                        motion.Status = "Closed";
                        changesMade = true;
                    }
                }
            }

            if (changesMade) await _context.SaveChangesAsync();
            return motions;
        }

        // GET: api/Motions/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMotion(int id)
        {
            var motion = await _context.Motions
                .Include(m => m.ProposedBy)
                .Include(m => m.SecondedBy)
                .Include(m => m.Votes)
                    .ThenInclude(v => v.Voter)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (motion == null) return NotFound();

            // Check if it expired right now
            await CheckExpiration(motion);

            return Ok(motion);
        }

        // POST: api/Motions
        // DynaFormDB will POST directly to this endpoint based on your apiurlpostfix="Motions"
        [HttpPost]
        public async Task<IActionResult> CreateMotion([FromBody] Motion motion)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            motion.ProposedOn = DateTime.UtcNow;
            motion.Status = "Proposed";
            
            _context.Motions.Add(motion);
            await _context.SaveChangesAsync();

            return Ok(motion);
        }

        // POST: api/Motions/5/second
        [HttpPost("{id}/second")]
        public async Task<IActionResult> SecondMotion(int id, [FromBody] int userId)
        {
            var motion = await _context.Motions.FindAsync(id);
            if (motion == null) return NotFound();

            if (motion.Status != "Proposed") 
                return BadRequest("Motion is already seconded or closed.");

            if (motion.ProposedById == userId) 
                return BadRequest("Proposer cannot second their own motion.");

            motion.SecondedById = userId;
            motion.SecondedOn = DateTime.UtcNow;
            motion.Status = "Seconded"; // Opens voting

            await _context.SaveChangesAsync();
            return Ok(motion);
        }

        public class VoteRequest { public int UserId { get; set; } public string VoteValue { get; set; } }

        // POST: api/Motions/5/vote
        [HttpPost("{id}/vote")]
        public async Task<IActionResult> VoteMotion(int id, [FromBody] VoteRequest request)
        {
            var motion = await _context.Motions.FindAsync(id);
            if (motion == null) return NotFound();
            
            // Final safety check against expired motion before catching a vote
            await CheckExpiration(motion);

            if (motion.Status != "Seconded") 
                return BadRequest("Motion is not open for voting.");

            var existingVote = await _context.MotionVotes
                .FirstOrDefaultAsync(v => v.MotionId == id && v.VoterId == request.UserId);

            if (existingVote != null) return BadRequest("User has already voted.");

            var vote = new MotionVote
            {
                MotionId = id,
                VoterId = request.UserId,
                VoteValue = request.VoteValue,
                VotedOn = DateTime.UtcNow
            };

            _context.MotionVotes.Add(vote);
            await _context.SaveChangesAsync();
            return Ok(vote);
        }
    }
}