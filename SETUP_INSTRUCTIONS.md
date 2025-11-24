# PureLedger Setup Instructions

## Prerequisites
- .NET 8.0 SDK
- Node.js 18+ and npm
- PostgreSQL database

## Backend Setup

1. **Navigate to backend directory**
   ```bash
   cd PureLedger/PureLedger
   ```

2. **Restore packages**
   ```bash
   dotnet restore
   ```

3. **Configure database connection**
   - Edit `appsettings.json`
   - Update the `ConnectionStrings:LedgerDb` with your PostgreSQL connection string

4. **Configure JWT settings** (optional, defaults provided)
   - Edit `appsettings.json`
   - Update JWT secret key, issuer, audience, and expiry minutes

5. **Run database migrations** (if needed)
   ```bash
   dotnet ef database update
   ```

6. **Run the backend**
   ```bash
   dotnet run
   ```
   Backend will run on `https://localhost:5000` or `http://localhost:5000`

## Frontend Setup

1. **Navigate to frontend directory**
   ```bash
   cd pureledger-frontend
   ```

2. **Install dependencies**
   ```bash
   npm install
   ```

3. **Create environment file**
   Create `.env` file in `pureledger-frontend` directory:
   ```
   VITE_API_BASE_URL=http://localhost:5000/api
   ```
   Or for HTTPS:
   ```
   VITE_API_BASE_URL=https://localhost:5000/api
   ```

4. **Run the frontend**
   ```bash
   npm run dev
   ```
   Frontend will run on `http://localhost:5173`

## First Login

Default admin credentials (if configured):
- Username: `admin`
- Password: `admin123`

**Note:** Change default credentials in production!

## Development Notes

- Backend API runs on port 5000 (HTTP) or 5001 (HTTPS)
- Frontend runs on port 5173
- CORS is configured to allow requests from frontend
- JWT tokens are stored in localStorage
- Token expiry is set to 24 hours by default

## Troubleshooting

### CORS Issues
- Ensure backend CORS configuration includes frontend URL
- Check `appsettings.json` for `Cors:AllowedOrigins`

### Authentication Issues
- Clear localStorage and try logging in again
- Check browser console for errors
- Verify JWT secret key is configured

### API Connection Issues
- Verify backend is running
- Check `VITE_API_BASE_URL` in `.env` file
- Ensure CORS is properly configured

## Production Deployment

1. **Backend**
   - Update `appsettings.json` with production database connection
   - Set strong JWT secret key
   - Configure CORS with production frontend URL
   - Build: `dotnet publish -c Release`

2. **Frontend**
   - Update `.env.production` with production API URL
   - Build: `npm run build`
   - Deploy `dist` folder to web server

## API Documentation

All API endpoints follow RESTful conventions:
- GET - Retrieve data
- POST - Create new resource
- PUT - Update existing resource
- DELETE - Delete resource

All endpoints require authentication except `/api/account/login`.

Response format:
```json
{
  "success": true,
  "data": {...},
  "message": "Success message"
}
```

Error format:
```json
{
  "success": false,
  "message": "Error message",
  "errors": ["Error 1", "Error 2"]
}
```

