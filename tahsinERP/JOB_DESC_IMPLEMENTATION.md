# Job Descriptions (JOB_DESC) Implementation

## Overview
This implementation creates a complete CRUD system for Job Descriptions (JOB_DESC) similar to STD_COMPANY_STANDARDS, with inline Word document editing capabilities.

## Files Created

### 1. Database Migration
- **Location**: `Database_Migrations/JOB_DESC_Create.sql`
- **Contents**:
  - Creates `JOB_DESC` table with version management (DECIMAL(5,2) for versions like 1.00, 1.01, etc.)
  - Links to `HR_JOB_DESCRIPTIONS` table
  - Creates necessary indexes for performance
  - Creates permission modules for all CRUD operations

### 2. Model
- **Location**: `Models/JOB_DESC.cs`
- **Properties**:
  - DocumentID (Primary Key, links to DOC_DOCUMENTS)
  - JobDescriptionCode
  - JobTitle
  - Department
  - VersionNo (DECIMAL(5,2))
  - Notes
  - HRJobDescriptionID (Nullable, links to HR_JOB_DESCRIPTIONS)

### 3. ViewModels
- **Location**: `ViewModels/JobDescViewModel.cs`
- **ViewModels**:
  - `JobDescIndexViewModel` - For Index page
  - `JobDescCreateViewModel` - For Create page
  - `JobDescEditViewModel` - For Edit page
  - `JobDescDetailsViewModel` - For Details page

### 4. Controller
- **Location**: `Controllers/JobDescController.cs`
- **Actions**:
  - `Index()` - List all job descriptions (fast loading, no .docx files)
  - `Create()` - Create new job description
  - `Edit(long? id)` - Edit existing (creates new version: v1.00 + 0.01 = v1.01)
  - `Details(long? id)` - View details and version history
  - `Delete(long? id)` - Delete confirmation
  - `DeleteConfirmed(long id)` - Delete action
  - `DownloadFile(long? fileId)` - Download .docx file (lazy loading)
  - `GetTemplate()` - Get template .docx file

### 5. Views
- **Location**: `Views/JobDesc/`
- **Files**:
  - `Index.cshtml` - DataTable with all job descriptions
  - `Create.cshtml` - Create form with Quill rich text editor
  - `Edit.cshtml` - Edit form with Quill editor (loads existing content)
  - `Details.cshtml` - Details view with version history
  - `Delete.cshtml` - Delete confirmation page

### 6. Service
- **Location**: `Services/WordDocumentConverterService.cs`
- **Methods**:
  - `ConvertHtmlToDocx(string htmlContent)` - Converts HTML (from Quill) to .docx
  - `ConvertDocxToHtml(byte[] docxBytes)` - Converts .docx to HTML (for editing)
  - `CreateEmptyDocx()` - Creates empty .docx file
  - `LoadTemplateDocx(string templatePath)` - Loads template .docx file

### 7. Database Context Update
- **Location**: `Models/DBTHSNModel.Context.cs`
- Added: `DbSet<JOB_DESC>`

## Key Features

### 1. Inline Word Editor
- Uses **Quill.js** (free, open-source rich text editor)
- Converts HTML to .docx on save using DocumentFormat.OpenXml
- Converts .docx to HTML when loading for editing
- No paid dependencies required

### 2. Version Management
- Automatic version increment: v1.00 → v1.01 → v1.02 (increments by 0.01)
- All versions are stored and viewable in Details page
- Old versions remain accessible for download

### 3. Performance Optimization
- **Index page**: Only loads metadata (no .docx files), uses DataTable for fast rendering
- **Details page**: Lazy loading - .docx files only downloaded when user clicks download button
- Database indexes created for fast searching

### 4. HR Integration
- Link to HR_JOB_DESCRIPTIONS table
- Dropdown in Create/Edit forms to select HR job description
- Shows linked HR job description in Details view

### 5. Template Support
- Template .docx file can be placed in `~/App_Data/Templates/JobDescriptionTemplate.docx`
- Template is automatically loaded when creating new document
- If template doesn't exist, starts with empty document

### 6. Permissions
- Full permission system integrated
- Permission modules created for all actions:
  - Index, Create, Edit, Details, Delete, DownloadFile, GetTemplate, LinkToHRJobDescription

## Setup Instructions

### 1. Run Database Migration
Execute the SQL script:
```sql
Database_Migrations/JOB_DESC_Create.sql
```

### 2. Update Entity Framework Model (if using Database First)
If you're using Entity Framework Database First approach, you need to:
1. Open Visual Studio
2. Open `Models/DBTHSNModel.edmx`
3. Right-click → "Update Model from Database"
4. Select the new `JOB_DESC` table
5. Save and rebuild

**Note**: The DbSet has already been added to `DBTHSNModel.Context.cs`, but you may need to regenerate the model.

### 3. Create Template Directory (Optional)
Create the template directory and add a template file:
```
~/App_Data/Templates/JobDescriptionTemplate.docx
```

### 4. Set Permissions
Go to Admin Panel → Permissions and assign permissions for "Job Descriptions" module to appropriate roles.

## Usage

### Creating a New Job Description
1. Navigate to JobDesc → Create
2. Enter Job Description Code (required)
3. Enter Job Title, Department (optional)
4. Optionally link to HR Job Description
5. Edit content in the rich text editor
6. Click Save

### Editing a Job Description
1. Navigate to JobDesc → Index
2. Click Edit on the desired item
3. Make changes in the editor
4. Click Save (creates new version automatically)

### Viewing Details
1. Navigate to JobDesc → Index
2. Click on the Job Description Code or Details button
3. View all information and version history
4. Download .docx file by clicking download button

## Technical Details

### Word Document Storage
- Documents are stored in `DOC_FILES` table
- File content stored as `varbinary(max)`
- File metadata (name, size, mime type) stored in `DOC_FILES`
- Main document version marked with `IsMain = true`

### HTML to DOCX Conversion
- Uses DocumentFormat.OpenXml library (already in project)
- Converts HTML paragraphs to Word paragraphs
- Preserves basic text formatting
- Simple conversion - complex formatting may be simplified

### DOCX to HTML Conversion
- Extracts text from Word paragraphs
- Converts to HTML `<p>` tags
- Used for loading existing documents into Quill editor

## Future Enhancements (Optional)

1. **Better HTML to DOCX conversion**: Use more advanced library to preserve formatting, tables, images
2. **Template variables**: Allow placeholder variables in templates
3. **Document comparison**: Compare different versions side-by-side
4. **Export to PDF**: Add PDF export functionality
5. **Document approval workflow**: Add approval process for job descriptions
6. **Advanced search**: Full-text search within document content

## Notes

- The system is designed to be fast and efficient
- Quill editor is free and open-source
- DocumentFormat.OpenXml is already included in the project
- All code follows the existing codebase patterns and conventions
- Permission system fully integrated
- Logging integrated for all operations

