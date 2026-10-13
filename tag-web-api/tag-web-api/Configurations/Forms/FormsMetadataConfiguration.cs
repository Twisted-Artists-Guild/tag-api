// <copyright file="FormsMetadataConfiguration.cs" company="Twisted Artists Guild">
// Copyright © Twisted Artists Guild. All rights reserved
// </copyright>

namespace TAGWEBAPI.Models.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using TAGWEBAPI.Models;

    public class Forms_MetadataConfiguration : IEntityTypeConfiguration<Forms_Metadata>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<Forms_Metadata> builder)
        {
            builder.HasKey(f => f.Forms_MetadataID);

            builder.Property(f => f.APIURLpostfix)
                .HasMaxLength(255)
                .IsRequired();

            builder.Property(f => f.FormBody)
                .HasMaxLength(255);

            builder.Property(f => f.FormStyle)
                .HasMaxLength(255);

            builder.Property(f => f.H1)
                .HasMaxLength(255);

            builder.Property(f => f.H2)
                .HasMaxLength(255);

            builder.Property(f => f.H3)
                .HasMaxLength(255);

            builder.Property(f => f.Name)
                .HasMaxLength(255)
                .IsRequired(false);

            builder.Property(f => f.RequestType)
                .HasMaxLength(25);

            builder.Property(f => f.SegmentationType)
                .HasMaxLength(100);

            builder.HasMany(f => f.Forms_Fields)
                .WithOne(ff => ff.Forms_Metadata)
                .HasForeignKey(ff => ff.Forms_MetadataID);

            SeedData(builder);
        }

        private static void SeedData(EntityTypeBuilder<Forms_Metadata> builder)
        {
            builder.HasData(
                new Forms_Metadata { Forms_MetadataID = 1, Name = "testform", APIURLpostfix = "broken_on_purpose", H1 = "h1", H2 = "h2", H3 = "h3", FormBody = "Entery data here:", RequestType = "add" },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 2, 
                    Name = "UploadPictureForm1", 
                    APIURLpostfix = "picture", 
                    H1 = "Picture Upload", 
                    H2 = "Upload your pictures", 
                    H3 = "Adding a new picture", 
                    FormBody = "Please provide details about your image:", 
                    FormStyle = "formstyle test", 
                    RequestType = "add" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 3, 
                    Name = "ArtistForm1", 
                    APIURLpostfix = "artist", 
                    H1 = "Artist Profile", 
                    H2 = "Create or Update Your Artist Profile", 
                    H3 = "Tell us about yourself or your group", 
                    FormBody = "Fill out the information below to create your artist profile. The title and slug are required.", 
                    FormStyle = "formstyle test", 
                    RequestType = "add/update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 5, 
                    Name = "UpdateFormsMeta", 
                    APIURLpostfix = "forms_metadata", 
                    H1 = "Form Metadata Editor", 
                    H2 = "Create or Update Form Metadata", 
                    H3 = "Define form properties", 
                    FormBody = "Use this form to configure form metadata:", 
                    FormStyle = "formstyle test DB", 
                    RequestType = "update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 6, 
                    Name = "UpdateFormsFields", 
                    APIURLpostfix = "forms_fields", 
                    H1 = "Form Fields Editor", 
                    H2 = "Create or Update Form Fields", 
                    H3 = "Define form field properties", 
                    FormBody = "Use this form to configure form fields:", 
                    FormStyle = "formstyle test DB", 
                    RequestType = "update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 7, 
                    Name = "EventForm1", 
                    APIURLpostfix = "event", 
                    H1 = "Event Information", 
                    H2 = "Create or Update Event", 
                    H3 = "Tell us about your event", 
                    FormBody = "Enter the details for your event. Title, description, start/end times, doors time, capacity and minimum age are required.", 
                    FormStyle = "formstyle test", 
                    RequestType = "add/update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 8, 
                    Name = "BlogForm1", 
                    APIURLpostfix = "blog", 
                    H1 = "Blog Post", 
                    H2 = "Create or Update Blog Post", 
                    H3 = "Share your thoughts", 
                    FormBody = "Enter the details for your blog post. Title, body, and byline are required.", 
                    FormStyle = "formstyle test", 
                    RequestType = "add/update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 9, 
                    Name = "UserForm1", 
                    APIURLpostfix = "user", 
                    H1 = "User Profile", 
                    H2 = "Create or Update User Information", 
                    H3 = "Personal Details", 
                    FormBody = "Enter your user information. Username and email are required.", 
                    FormStyle = "formstyle test", 
                    RequestType = "add/update" 
                },
                
                new Forms_Metadata { 
                    Forms_MetadataID = 10, 
                    Name = "ListingForm1", 
                    APIURLpostfix = "listingByID", 
                    H1 = "Listing Information", 
                    H2 = "Create or Update Listing", 
                    H3 = "Tell us about your art", 
                    FormBody = "Enter the details for your listing. Title is required, and a path will be generated automatically.", 
                    FormStyle = "formstyle test", 
                    RequestType = "add/update" 
                }
            );
        }
    }
}
