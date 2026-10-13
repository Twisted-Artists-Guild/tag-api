// <copyright file="FormsFieldConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class Forms_FieldConfiguration : IEntityTypeConfiguration<Forms_Field>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Forms_Field> builder)
        {
            builder.HasKey(f => f.Forms_FieldID);

            builder.Property(f => f.Name)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(f => f.ClassName)
                .IsRequired(false)
                .HasMaxLength(255);

            builder.Property(f => f.DefaultValue)
                .HasMaxLength(255);

            builder.Property(f => f.Height)
                .HasMaxLength(255);

            builder.Property(f => f.Label)
                .HasMaxLength(255);

            builder.Property(f => f.Placeholder)
                .HasMaxLength(255);

            builder.Property(f => f.Type)
                .HasMaxLength(255);

            builder.Property(f => f.RegexValidationPattern)
                .HasMaxLength(255);

            builder.Property(f => f.Width)
                .HasMaxLength(255);

            builder.Property(f => f.IsReadOnly)
                .HasDefaultValue(false);

            builder.Property(f => f.IsRequired)
                .HasDefaultValue(false);

            builder.Property(f => f.Forms_MetadataID).IsRequired();

            builder.HasOne(f => f.Forms_Metadata)
                .WithMany(m => m.Forms_Fields)
                .HasForeignKey(f => f.Forms_MetadataID)
                .OnDelete(DeleteBehavior.SetNull);

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<Forms_Field> builder)
        {
            builder.HasData(
                // Test Form - standard sizes 
                new Forms_Field { Forms_FieldID = 1, Forms_MetadataID = 1, FieldOrder = 1, Type = "text", Name = "username", Width = "100%", Height = "40px", DefaultValue = "default value test", Placeholder = "Placeholder Test" },
                new Forms_Field { Forms_FieldID = 2, Forms_MetadataID = 1, FieldOrder = 2, Type = "textarea", Name = "password", Width = "100%", Height = "80px", DefaultValue = "default value test", Placeholder = "Placeholder Test" },

                // UploadPictureForm1 (Picture entity)
                new Forms_Field { Forms_FieldID = 3, Forms_MetadataID = 2, Name = "context", ClassName = "styles.input", Type = "text", Placeholder = "Artist, listing, event, etc", DefaultValue = "", Label = "Context", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 4, Forms_MetadataID = 2, Name = "path", ClassName = "styles.input", Type = "text", Placeholder = "Slug", DefaultValue = "", Label = "Slug", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 5, Forms_MetadataID = 2, Name = "description", ClassName = "styles.input", Type = "text", Placeholder = "Description", DefaultValue = "", Label = "Description", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 6, Forms_MetadataID = 2, Name = "title", ClassName = "styles.input", Type = "text", Placeholder = "Title", DefaultValue = "", Label = "Title", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 7, Forms_MetadataID = 2, Name = "alttext", ClassName = "styles.input", Type = "text", Placeholder = "Alt Text", DefaultValue = "", Label = "Alt Text", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 8, Forms_MetadataID = 2, Name = "url", ClassName = "styles.input", Type = "text", Placeholder = "URL", DefaultValue = "", Label = "URL", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 9, Forms_MetadataID = 2, Name = "created", ClassName = "styles.input", Type = "text", Placeholder = "Created", DefaultValue = "", Label = "Created", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 10, Forms_MetadataID = 2, Name = "height", ClassName = "styles.input", Type = "text", Placeholder = "Height", DefaultValue = "0", Label = "Height", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 11, Forms_MetadataID = 2, Name = "width", ClassName = "styles.input", Type = "text", Placeholder = "Width", DefaultValue = "0", Label = "Width", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 12, Forms_MetadataID = 2, Name = "thumbnailurl", ClassName = "styles.input", Type = "text", Placeholder = "Thumbnail URL", DefaultValue = "", Label = "Thumbnail URL", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 13, Forms_MetadataID = 2, Name = "thumbnailheight", ClassName = "styles.input", Type = "text", Placeholder = "Thumbnail Height", DefaultValue = "0", Label = "Thumbnail Height", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 14, Forms_MetadataID = 2, Name = "thumbnailwidth", ClassName = "styles.input", Type = "text", Placeholder = "Thumbnail Width", DefaultValue = "0", Label = "Thumbnail Width", RegexValidationPattern = null },

                // ArtistForm1 (Artist entity) - only title and slug are required
                new Forms_Field { Forms_FieldID = 101, Forms_MetadataID = 3, Name = "path", ClassName = "styles.input", Type = "text", Placeholder = "Slug (similar to title)", DefaultValue = "", Label = "Slug", RegexValidationPattern = "validate_string", IsReadOnly = true },
                new Forms_Field { Forms_FieldID = 102, Forms_MetadataID = 3, Name = "title", ClassName = "styles.input", Type = "text", Placeholder = "Artist or Group Name", DefaultValue = "", Label = "Title", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 103, Forms_MetadataID = 3, Name = "byline", ClassName = "styles.input", Type = "textarea", Placeholder = "Short description (appears under title)", DefaultValue = "", Label = "Byline", Height = "3", Width = "100%", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 104, Forms_MetadataID = 3, Name = "statement", ClassName = "styles.input", Type = "textarea", Placeholder = "Detailed artist statement", DefaultValue = "", Label = "Statement", Height = "5", Width = "100%", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 105, Forms_MetadataID = 3, Name = "seotags", ClassName = "styles.input", Type = "text", Placeholder = "Keywords separated by commas", DefaultValue = "", Label = "SEO Search Terms", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 107, Forms_MetadataID = 3, Name = "since", ClassName = "styles.input", Type = "datetime", Placeholder = "Since", DefaultValue = "", Label = "Since", RegexValidationPattern = null },

                // EventForm1 (Event entity)
                new Forms_Field { Forms_FieldID = 201, Forms_MetadataID = 7, Name = "title", ClassName = "styles.input", Type = "text", Placeholder = "Event Title", DefaultValue = "", Label = "Title", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 202, Forms_MetadataID = 7, Name = "description", ClassName = "styles.input", Type = "textarea", Placeholder = "Event Description", DefaultValue = "", Label = "Description", Height = "10", Width = "100%", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 203, Forms_MetadataID = 7, Name = "starttime", ClassName = "styles.input", Type = "datetime-local", Placeholder = "Start Time", DefaultValue = "", Label = "Start Time", RegexValidationPattern = "validate_datetime", IsRequired = true },
                new Forms_Field { Forms_FieldID = 204, Forms_MetadataID = 7, Name = "endtime", ClassName = "styles.input", Type = "datetime-local", Placeholder = "End Time", DefaultValue = "", Label = "End Time", RegexValidationPattern = "validate_datetime", IsRequired = true },
                new Forms_Field { Forms_FieldID = 205, Forms_MetadataID = 7, Name = "doors", ClassName = "styles.input", Type = "datetime-local", Placeholder = "Doors", DefaultValue = "", Label = "Doors", RegexValidationPattern = "validate_datetime", IsRequired = true },
                new Forms_Field { Forms_FieldID = 206, Forms_MetadataID = 7, Name = "pointofcontact", ClassName = "styles.input", Type = "text", Placeholder = "Name and contact details", DefaultValue = "", Label = "Point of Contact", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 207, Forms_MetadataID = 7, Name = "note", ClassName = "styles.input", Type = "textarea", Placeholder = "Additional notes", DefaultValue = "", Label = "Notes", Height = "3", Width = "100%", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 208, Forms_MetadataID = 7, Name = "cost", ClassName = "styles.input", Type = "number", Placeholder = "Cost", DefaultValue = "0.00", Label = "Cost", RegexValidationPattern = "validate_number" },
                new Forms_Field { Forms_FieldID = 209, Forms_MetadataID = 7, Name = "maxoccupancy", ClassName = "styles.input", Type = "number", Placeholder = "Max Occupancy", DefaultValue = "0", Label = "Max Occupancy", RegexValidationPattern = "validate_number", IsRequired = true },
                new Forms_Field { Forms_FieldID = 210, Forms_MetadataID = 7, Name = "minimumage", ClassName = "styles.input", Type = "number", Placeholder = "Minimum Age", DefaultValue = "0", Label = "Minimum Age", RegexValidationPattern = "validate_number", IsRequired = true },
                new Forms_Field { Forms_FieldID = 211, Forms_MetadataID = 7, Name = "path", ClassName = "styles.input", Type = "text", Placeholder = "Path (similar to title)", DefaultValue = "", Label = "Path", RegexValidationPattern = "validate_string", IsReadOnly = true },

                // BlogForm1 (Blog entity)
                new Forms_Field { Forms_FieldID = 301, Forms_MetadataID = 8, Name = "title", ClassName = "styles.input", Type = "text", Placeholder = "Blog Title", DefaultValue = "", Label = "Title", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 302, Forms_MetadataID = 8, Name = "body", ClassName = "styles.input", Type = "textarea", Placeholder = "Blog Content", DefaultValue = "", Label = "Content", Height = "15", Width = "100%", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 303, Forms_MetadataID = 8, Name = "byline", ClassName = "styles.input", Type = "textarea", Placeholder = "Short preview or summary", DefaultValue = "", Label = "Byline", Height = "3", Width = "100%", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 304, Forms_MetadataID = 8, Name = "path", ClassName = "styles.input", Type = "text", Placeholder = "Path (similar to title)", DefaultValue = "", Label = "Path", RegexValidationPattern = "validate_string", IsReadOnly = true },

                // UserForm1 (User entity)
                new Forms_Field { Forms_FieldID = 401, Forms_MetadataID = 9, Name = "username", ClassName = "styles.input", Type = "text", Placeholder = "Username", DefaultValue = "default value", Label = "Username", RegexValidationPattern = "validate_string"},
                new Forms_Field { Forms_FieldID = 402, Forms_MetadataID = 9, Name = "emailone", ClassName = "styles.input", Type = "email", Placeholder = "Email", DefaultValue = "default value", Label = "Email", RegexValidationPattern = "validate_email" },
                new Forms_Field { Forms_FieldID = 403, Forms_MetadataID = 9, Name = "emailtwo", ClassName = "styles.input", Type = "email", Placeholder = "Email Two", DefaultValue = "", Label = "Email Two", RegexValidationPattern = "validate_email" },
                new Forms_Field { Forms_FieldID = 404, Forms_MetadataID = 9, Name = "firstname", ClassName = "styles.input", Type = "text", Placeholder = "First Name", DefaultValue = "", Label = "First Name", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 405, Forms_MetadataID = 9, Name = "famname", ClassName = "styles.input", Type = "text", Placeholder = "Family Name", DefaultValue = "", Label = "Family Name", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 406, Forms_MetadataID = 9, Name = "companyname", ClassName = "styles.input", Type = "text", Placeholder = "Company Name", DefaultValue = "", Label = "Company Name", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 407, Forms_MetadataID = 9, Name = "companytitle", ClassName = "styles.input", Type = "text", Placeholder = "Company Title", DefaultValue = "", Label = "Company Title", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 408, Forms_MetadataID = 9, Name = "nationality", ClassName = "styles.input", Type = "text", Placeholder = "Nationality", DefaultValue = "", Label = "Nationality", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 409, Forms_MetadataID = 9, Name = "preferredname", ClassName = "styles.input", Type = "text", Placeholder = "Preferred Name", DefaultValue = "", Label = "Preferred Name", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 410, Forms_MetadataID = 9, Name = "gender", ClassName = "styles.input", Type = "text", Placeholder = "Gender", DefaultValue = "", Label = "Gender", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 411, Forms_MetadataID = 9, Name = "bannedreason", ClassName = "styles.input", Type = "text", Placeholder = "Banned Reason", DefaultValue = "", Label = "Banned Reason", RegexValidationPattern = "validate_string" },

                // ListingForm1 (Listing entity)
                new Forms_Field { Forms_FieldID = 501, Forms_MetadataID = 10, Name = "title", ClassName = "styles.input", Type = "text", Placeholder = "Listing Title", DefaultValue = "", Label = "Title", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 502, Forms_MetadataID = 10, Name = "description", ClassName = "styles.input", Type = "textarea", Placeholder = "Detailed description", DefaultValue = "", Label = "Description", Height = "5", Width = "100%" },
                new Forms_Field { Forms_FieldID = 503, Forms_MetadataID = 10, Name = "catalogueid", ClassName = "styles.input", Type = "text", Placeholder = "Catalogue ID", DefaultValue = "", Label = "Catalogue ID" },
                new Forms_Field { Forms_FieldID = 504, Forms_MetadataID = 10, Name = "credits", ClassName = "styles.input", Type = "textarea", Placeholder = "Credits for contributors", DefaultValue = "", Label = "Credits", Height = "3", Width = "100%" },
                new Forms_Field { Forms_FieldID = 505, Forms_MetadataID = 10, Name = "culture", ClassName = "styles.input", Type = "text", Placeholder = "Cultural origin", DefaultValue = "", Label = "Culture" },
                new Forms_Field { Forms_FieldID = 506, Forms_MetadataID = 10, Name = "date", ClassName = "styles.input", Type = "text", Placeholder = "Creation date", DefaultValue = "", Label = "Date" },
                new Forms_Field { Forms_FieldID = 507, Forms_MetadataID = 10, Name = "department", ClassName = "styles.input", Type = "text", Placeholder = "Department", DefaultValue = "", Label = "Department" },
                new Forms_Field { Forms_FieldID = 508, Forms_MetadataID = 10, Name = "locale", ClassName = "styles.input", Type = "text", Placeholder = "Geographic location", DefaultValue = "", Label = "Locale" },
                new Forms_Field { Forms_FieldID = 509, Forms_MetadataID = 10, Name = "locus", ClassName = "styles.input", Type = "text", Placeholder = "Specific location", DefaultValue = "", Label = "Locus" },
                new Forms_Field { Forms_FieldID = 510, Forms_MetadataID = 10, Name = "medium", ClassName = "styles.input", Type = "textarea", Placeholder = "Materials and techniques", DefaultValue = "", Label = "Medium", Height = "3", Width = "100%" },
                new Forms_Field { Forms_FieldID = 511, Forms_MetadataID = 10, Name = "period", ClassName = "styles.input", Type = "text", Placeholder = "Historical period", DefaultValue = "", Label = "Period" },
                new Forms_Field { Forms_FieldID = 512, Forms_MetadataID = 10, Name = "repository", ClassName = "styles.input", Type = "text", Placeholder = "Storage location", DefaultValue = "", Label = "Repository" },
                new Forms_Field { Forms_FieldID = 513, Forms_MetadataID = 10, Name = "rights", ClassName = "styles.input", Type = "text", Placeholder = "Copyright information", DefaultValue = "", Label = "Rights" },
                new Forms_Field { Forms_FieldID = 514, Forms_MetadataID = 10, Name = "taxjurisdiction", ClassName = "styles.input", Type = "text", Placeholder = "Tax Jurisdiction", DefaultValue = "", Label = "Tax Jurisdiction" },
                new Forms_Field { Forms_FieldID = 515, Forms_MetadataID = 10, Name = "path", ClassName = "styles.input", Type = "text", Placeholder = "Path (auto-generated from title)", DefaultValue = "", Label = "Path", RegexValidationPattern = "validate_string", IsReadOnly = true },

                // UpdateFormsMeta (Forms_MetadataID = 5) - metadata editor fields (IDs in 600s)
                new Forms_Field { Forms_FieldID = 601, Forms_MetadataID = 5, Name = "name", ClassName = "styles.input", Type = "text", Placeholder = "Form Name", DefaultValue = "", Label = "Form Name", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 602, Forms_MetadataID = 5, Name = "APIURLpostfix", ClassName = "styles.input", Type = "text", Placeholder = "API URL Postfix (e.g. artist)", DefaultValue = "", Label = "API URL Postfix", RegexValidationPattern = "validate_string" },
                new Forms_Field { Forms_FieldID = 603, Forms_MetadataID = 5, Name = "H1", ClassName = "styles.input", Type = "text", Placeholder = "H1 text", DefaultValue = "", Label = "H1", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 604, Forms_MetadataID = 5, Name = "H2", ClassName = "styles.input", Type = "text", Placeholder = "H2 text", DefaultValue = "", Label = "H2", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 605, Forms_MetadataID = 5, Name = "H3", ClassName = "styles.input", Type = "text", Placeholder = "H3 text", DefaultValue = "", Label = "H3", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 606, Forms_MetadataID = 5, Name = "FormBody", ClassName = "styles.input", Type = "textarea", Placeholder = "Form body / instructions", DefaultValue = "", Label = "Form Body", Height = "5", Width = "100%", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 607, Forms_MetadataID = 5, Name = "FormStyle", ClassName = "styles.input", Type = "text", Placeholder = "Form style class", DefaultValue = "", Label = "Form Style", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 608, Forms_MetadataID = 5, Name = "RequestType", ClassName = "styles.input", Type = "text", Placeholder = "Request type (add/update)", DefaultValue = "update", Label = "Request Type", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 609, Forms_MetadataID = 5, Name = "SegmentationType", ClassName = "styles.input", Type = "text", Placeholder = "Segmentation type", DefaultValue = "", Label = "Segmentation Type", RegexValidationPattern = null },

                // UpdateFormsFields (Forms_MetadataID = 6) - fields-editor controls (IDs in 700s)
                new Forms_Field { Forms_FieldID = 701, Forms_MetadataID = 6, Name = "Name", ClassName = "styles.input", Type = "text", Placeholder = "Field name/key", DefaultValue = "", Label = "Field Name", RegexValidationPattern = "validate_string", IsRequired = true },
                new Forms_Field { Forms_FieldID = 702, Forms_MetadataID = 6, Name = "Type", ClassName = "styles.input", Type = "text", Placeholder = "Input type (text,textarea,number,email)", DefaultValue = "text", Label = "Field Type", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 703, Forms_MetadataID = 6, Name = "Placeholder", ClassName = "styles.input", Type = "text", Placeholder = "Placeholder text", DefaultValue = "", Label = "Placeholder", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 704, Forms_MetadataID = 6, Name = "Label", ClassName = "styles.input", Type = "text", Placeholder = "Label text", DefaultValue = "", Label = "Label", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 705, Forms_MetadataID = 6, Name = "DefaultValue", ClassName = "styles.input", Type = "text", Placeholder = "Default value", DefaultValue = "", Label = "Default Value", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 706, Forms_MetadataID = 6, Name = "ClassName", ClassName = "styles.input", Type = "text", Placeholder = "CSS class", DefaultValue = "", Label = "Class Name", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 707, Forms_MetadataID = 6, Name = "Width", ClassName = "styles.input", Type = "text", Placeholder = "Width (e.g. 100%)", DefaultValue = "100%", Label = "Width", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 708, Forms_MetadataID = 6, Name = "Height", ClassName = "styles.input", Type = "text", Placeholder = "Height (e.g. 40px)", DefaultValue = "", Label = "Height", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 709, Forms_MetadataID = 6, Name = "RegexValidationPattern", ClassName = "styles.input", Type = "text", Placeholder = "Validation pattern key", DefaultValue = "", Label = "Regex Validation", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 710, Forms_MetadataID = 6, Name = "FieldOrder", ClassName = "styles.input", Type = "number", Placeholder = "Order (integer)", DefaultValue = "0", Label = "Field Order", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 711, Forms_MetadataID = 6, Name = "IsRequired", ClassName = "styles.input", Type = "checkbox", Placeholder = "", DefaultValue = "false", Label = "Is Required", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 712, Forms_MetadataID = 6, Name = "IsReadOnly", ClassName = "styles.input", Type = "checkbox", Placeholder = "", DefaultValue = "false", Label = "Is Read Only", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 713, Forms_MetadataID = 6, Name = "Min", ClassName = "styles.input", Type = "number", Placeholder = "Min (optional)", DefaultValue = "", Label = "Min", RegexValidationPattern = null },
                new Forms_Field { Forms_FieldID = 714, Forms_MetadataID = 6, Name = "Max", ClassName = "styles.input", Type = "number", Placeholder = "Max (optional)", DefaultValue = "", Label = "Max", RegexValidationPattern = null }
            );
        }
    }
}
