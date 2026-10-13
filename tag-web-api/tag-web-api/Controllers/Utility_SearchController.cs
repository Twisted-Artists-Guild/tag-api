using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using TAGWEBAPI.Data;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers
{
    /// <summary>
    /// Controller for handling cross-model search functionality.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class Utility_SearchController : ControllerBase
    {
        private readonly TAGDBContext _context;
        
        public Utility_SearchController(TAGDBContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Searches for one or more keyword tokens across multiple entities.
        /// </summary>
        /// <param name="keyword">A space-delimited keyword string.</param>
        /// <returns>A list of search results with snippets around each match.</returns>
        [HttpGet("search")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult Search(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return BadRequest("Keyword cannot be empty.");
            }

            var tokens = keyword.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Initialize result groups
            var blogResults = new List<SearchResult>();
            var artistResults = new List<SearchResult>();
            var listingResults = new List<SearchResult>();
            var eventResults = new List<SearchResult>();

            // Search in Blogs
            var blogCandidates = _context.Blogs
                .Where(b => tokens.Any(token =>
                    b.Body.Contains(token) ||
                    b.Title.Contains(token) ||
                    b.Byline.Contains(token)))
                .Take(5) // Limit to top 5
                .ToList();

            foreach (var blog in blogCandidates)
            {
                foreach (var token in tokens)
                {
                    if (!string.IsNullOrEmpty(blog.Body) && IsMatch(blog.Body, token))
                    {
                        blogResults.Add(new SearchResult
                        {
                            Source = "Blog",
                            Id = blog.BlogID.ToString(),
                            Field = "Body",
                            Snippet = GetSnippet(blog.Body, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(blog.Title) && IsMatch(blog.Title, token))
                    {
                        blogResults.Add(new SearchResult
                        {
                            Source = "Blog",
                            Id = blog.BlogID.ToString(),
                            Field = "Title",
                            Snippet = GetSnippet(blog.Title, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(blog.Byline) && IsMatch(blog.Byline, token))
                    {
                        blogResults.Add(new SearchResult
                        {
                            Source = "Blog",
                            Id = blog.BlogID.ToString(),
                            Field = "Byline",
                            Snippet = GetSnippet(blog.Byline, token)
                        });
                    }
                }
            }

            // Search in Artists
            var artistCandidates = _context.Artists
                .Where(a => tokens.Any(token =>
                    (a.Biography != null && a.Biography.Contains(token)) ||
                    (a.Byline != null && a.Byline.Contains(token)) ||
                    (a.Title != null && a.Title.Contains(token)) ||
                    (a.Path != null && a.Path.Contains(token)) ||
                    (a.SEOTags != null && a.SEOTags.Contains(token)) ||
                    (a.Statement != null && a.Statement.Contains(token))))
                .Take(5) // Limit to top 5
                .ToList();

            foreach (var artist in artistCandidates)
            {
                foreach (var token in tokens)
                {
                    if (!string.IsNullOrEmpty(artist.Biography) && IsMatch(artist.Biography, token))
                    {
                        artistResults.Add(new SearchResult
                        {
                            Source = "Artist",
                            Id = artist.ArtistID.ToString(),
                            Field = "Biography",
                            Snippet = GetSnippet(artist.Biography, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(artist.Byline) && IsMatch(artist.Byline, token))
                    {
                        artistResults.Add(new SearchResult
                        {
                            Source = "Artist",
                            Id = artist.ArtistID.ToString(),
                            Field = "Byline",
                            Snippet = GetSnippet(artist.Byline, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(artist.Title) && IsMatch(artist.Title, token))
                    {
                        artistResults.Add(new SearchResult
                        {
                            Source = "Artist",
                            Id = artist.ArtistID.ToString(),
                            Field = "Title",
                            Snippet = GetSnippet(artist.Title, token)
                        });
                    }
                }
            }

            // Search in Listings
            var listingCandidates = _context.Listings
                .Where(l => tokens.Any(token =>
                    (l.Description != null && l.Description.Contains(token)) ||
                    (l.Title != null && l.Title.Contains(token)) ||
                    (l.Culture != null && l.Culture.Contains(token)) ||
                    (l.Medium != null && l.Medium.Contains(token)) ||
                    (l.Path != null && l.Path.Contains(token)) ||
                    (l.ArtCategory != null && l.ArtCategory.Category.Contains(token))))
                .Take(10) // Limit to top 10
                .ToList();

            foreach (var listing in listingCandidates)
            {
                foreach (var token in tokens)
                {
                    if (!string.IsNullOrEmpty(listing.Description) && IsMatch(listing.Description, token))
                    {
                        listingResults.Add(new SearchResult
                        {
                            Source = "Listing",
                            Id = listing.ListingID.ToString(),
                            Field = "Description",
                            Snippet = GetSnippet(listing.Description, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(listing.Title) && IsMatch(listing.Title, token))
                    {
                        listingResults.Add(new SearchResult
                        {
                            Source = "Listing",
                            Id = listing.ListingID.ToString(),
                            Field = "Title",
                            Snippet = GetSnippet(listing.Title, token)
                        });
                    }
                }
            }

            // Search in Events
            var eventCandidates = _context.Events
                .Where(e => tokens.Any(token =>
                    (e.Description != null && e.Description.Contains(token)) ||
                    (e.Title != null && e.Title.Contains(token)) ||
                    (e.Note != null && e.Note.Contains(token)) ||
                    (e.Path != null && e.Path.Contains(token))))
                .Take(5) // Limit to top 5
                .ToList();

            foreach (var ev in eventCandidates)
            {
                foreach (var token in tokens)
                {
                    if (!string.IsNullOrEmpty(ev.Description) && IsMatch(ev.Description, token))
                    {
                        eventResults.Add(new SearchResult
                        {
                            Source = "Event",
                            Id = ev.EventID.ToString(),
                            Field = "Description",
                            Snippet = GetSnippet(ev.Description, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(ev.Title) && IsMatch(ev.Title, token))
                    {
                        eventResults.Add(new SearchResult
                        {
                            Source = "Event",
                            Id = ev.EventID.ToString(),
                            Field = "Title",
                            Snippet = GetSnippet(ev.Title, token)
                        });
                    }
                    if (!string.IsNullOrEmpty(ev.Note) && IsMatch(ev.Note, token))
                    {
                        eventResults.Add(new SearchResult
                        {
                            Source = "Event",
                            Id = ev.EventID.ToString(),
                            Field = "Note",
                            Snippet = GetSnippet(ev.Note, token)
                        });
                    }
                }
            }

            // Combine results into a grouped response
            var response = new
            {
                Blogs = blogResults,
                Artists = artistResults,
                Listings = listingResults,
                Events = eventResults
            };

            return Ok(response);
        }

        /// <summary>
        /// Extracts a snippet from the given text with a defined context length around the first occurrence of the keyword.
        /// </summary>
        private string GetSnippet(string text, string keyword, int contextLength = 25)
        {
            int index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return string.Empty;
            }
            int start = Math.Max(0, index - contextLength);
            int end = Math.Min(text.Length, index + keyword.Length + contextLength);
            string snippet = text.Substring(start, end - start);
            if (start > 0)
            {
                snippet = "..." + snippet;
            }
            if (end < text.Length)
            {
                snippet = snippet + "...";
            }
            return snippet;
        }

        /// <summary>
        /// Determines if the field is a match for the token using exact or fuzzy matching.
        /// </summary>
        private bool IsMatch(string field, string token)
        {
            // Return true if the token is found exactly (case-insensitive)
            if (field.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // For fuzzy matching, we compare against each word in the field.
            var words = field.Split(' ');
            foreach (var word in words)
            {
                // Using a basic Levenshtein distance (for demonstration).
                int distance = LevenshteinDistance(word.ToLower(), token.ToLower());
                // If the distance is within an acceptable threshold, we consider it a match.
                if (distance <= Math.Max(1, token.Length / 3))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the Levenshtein distance between two strings.
        /// </summary>
        private int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
                return t.Length;
            if (string.IsNullOrEmpty(t))
                return s.Length;

            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,      // Deletion
                                 d[i, j - 1] + 1),     // Insertion
                        d[i - 1, j - 1] + cost);       // Substitution
                }
            }
            return d[s.Length, t.Length];
        }
    }

    /// <summary>
    /// Represents a single search result.
    /// </summary>
    public class SearchResult
    {
        public string Source { get; set; } // e.g., "Blog", "Artist", etc.

        public string Id { get; set; }     // The unique identifier from the original record

        public string Field { get; set; }  // The name of the field that matched (e.g., "Title", "Body")

        public string Snippet { get; set; } // A substring of the field with context around the match
    }
}
