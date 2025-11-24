# PureLedger MVC to React + API Migration

## Overview
This project has been migrated from an MVC architecture to a modern React frontend with RESTful API backend using JWT authentication.

## Architecture

### Backend
- **Framework**: ASP.NET Core 8.0
- **Authentication**: JWT Bearer Tokens
- **Database**: PostgreSQL
- **ORM**: Entity Framework Core
- **Mapping**: AutoMapper

### Frontend
- **Framework**: React 19
- **Routing**: React Router v6
- **HTTP Client**: Axios
- **UI Framework**: React Bootstrap 5
- **Build Tool**: Vite

## Project Structure

```
PureLedger/
├── Controllers/
│   └── Api/              # All API controllers
│       ├── AccountApiController.cs
│       ├── HomeApiController.cs
│       ├── MetaDataApiController.cs
│       ├── ReportingApiController.cs
│       ├── DeliveryChallanApiController.cs
│       └── AdminApiController.cs
├── Services/
│   ├── JwtService.cs     # JWT token generation/validation
│   └── ...
├── Mapping/
│   └── MappingProfile.cs # AutoMapper profiles

pureledger-frontend/
├── src/
│   ├── components/       # Reusable components
│   ├── contexts/         # React contexts (Auth)
│   ├── pages/            # Page components
│   ├── services/         # API client
│   └── App.jsx
```

## Setup Instructions

### Backend Setup

1. **Install Dependencies**
   ```bash
   cd PureLedger
   dotnet restore
   ```

2. **Configure Database**
   - Update connection string in `appsettings.json`
   - Run migrations if needed

3. **Configure JWT**
   - Update JWT settings in `appsettings.json`:
   ```json
   {
     "Jwt": {
       "SecretKey": "YourSecretKeyHere",
       "Issuer": "PureLedger",
       "Audience": "PureLedger",
       "ExpiryMinutes": "1440"
     }
   }
   ```

4. **Run Backend**
   ```bash
   dotnet run
   ```

### Frontend Setup

1. **Install Dependencies**
   ```bash
   cd pureledger-frontend
   npm install
   ```

2. **Configure API URL**
   Create `.env` file:
   ```
   VITE_API_BASE_URL=http://localhost:5000/api
   ```

3. **Run Frontend**
   ```bash
   npm run dev
   ```

## API Endpoints

All API endpoints are prefixed with `/api/`

### Authentication
- `POST /api/account/login` - Login
- `POST /api/account/logout` - Logout
- `GET /api/account/me` - Get current user
- `GET /api/account/access` - Get user access permissions

### Home/Journal Entries
- `GET /api/home/entry-data` - Get entry page data
- `POST /api/home/entry` - Create/update journal entry
- `GET /api/home/edit-voucher/{id}` - Get voucher for editing
- And more...

### MetaData
- `GET /api/metadata/projects` - Get all projects
- `POST /api/metadata/projects` - Create project
- `PUT /api/metadata/projects/{id}` - Update project
- `DELETE /api/metadata/projects/{id}` - Delete project
- Similar endpoints for Categories, Accounts, Locations, Bookers

### Reporting
- `GET /api/reporting/aging-report` - Get aging report
- `GET /api/reporting/account-report` - Get account report
- And more...

### Delivery Challan
- `GET /api/deliverychallan/filtered-entries` - Get filtered entries
- `POST /api/deliverychallan/single-entry` - Create single entry
- `POST /api/deliverychallan/process-all` - Process batch
- And more...

### Admin
- `GET /api/admin/users` - Get all users
- `POST /api/admin/users` - Create user
- `PUT /api/admin/users/{id}` - Update user
- `DELETE /api/admin/users/{id}` - Delete user
- And more...

## Authentication Flow

1. User logs in via `POST /api/account/login`
2. Backend validates credentials and returns JWT token
3. Frontend stores token in localStorage
4. All subsequent API requests include token in Authorization header
5. Backend validates token on each request

## Key Features

- ✅ JWT-based authentication
- ✅ Role-based access control
- ✅ AutoMapper for DTO mapping
- ✅ CORS configured for React app
- ✅ Consistent API response format
- ✅ Error handling
- ✅ Loading states
- ✅ Modal dialogs
- ✅ Responsive design
- ✅ Accessibility improvements

## Migration Status

### ✅ Completed
- All API controllers created
- JWT authentication implemented
- AutoMapper configured
- React frontend foundation
- Shared components
- Login page
- Sample pages (Entry, Projects)

### 🚧 In Progress
- Remaining React pages (following established patterns)

## Development Notes

- All API responses follow format: `{ success: boolean, data?: any, message?: string, errors?: string[] }`
- Use `apiClient` from `services/api.js` for all API calls
- Use `useAuth` hook for authentication state
- Follow the Projects.jsx pattern for CRUD operations
- Use shared components (Modal, DataTable, etc.) for consistency

## Next Steps

1. Complete remaining React pages
2. Add comprehensive error handling
3. Add form validation
4. Add unit tests
5. Configure production settings
6. Deploy

