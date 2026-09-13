# Role-Based Access Control (RBAC) & JWT Authentication Implementation

## Overview
This document describes the complete RBAC and JWT authentication system implemented in the POS System API.

## Architecture

### Authentication Flow
1. **User Registration** (`POST /api/auth/register`)
   - Creates new user accounts with optional role assignment
   - Only SuperAdmin users can create SuperAdmin accounts
   - Default role is Cashier for security
   - Passwords hashed with BCrypt

2. **User Login** (`POST /api/auth/login`)
   - Validates credentials against hashed password
   - Generates JWT access token with role claims
   - Returns TokenResponseDto containing token, user info, and expiry
   - Updates LastLoginAt timestamp

3. **Token Validation** (`POST /api/auth/validate-token`)
   - Validates JWT without authenticating
   - Checks signature, issuer, audience, and expiry
   - Useful for pre-flight checks

### Authorization Model

#### Role Hierarchy
- **SuperAdmin** (ID: 1)
  - Full administrative access
  - Can create/update/delete products
  - Can create SuperAdmin accounts
  - Can view administrative reports
  - Can manage shifts and cash drawer
  - Default credentials: `admin` / `Admin@123456`

- **Cashier** (ID: 2)
  - Can perform sales transactions
  - Can view product catalog
  - Can manage shopping cart and checkout
  - Can view own shift information
  - Default credentials: `cashier` / `Cashier@123456`

## Database Schema Updates

### Users Table
```sql
ALTER TABLE Users ADD COLUMN Role VARCHAR(50) NOT NULL DEFAULT 'Cashier';
CREATE INDEX idx_User_Role ON Users(Role);
```

New columns:
- `Role` (VARCHAR(50)) - User's role for RBAC
- `LastLoginAt` (DATETIME NULL) - Timestamp of last successful login

### Default Seed Data
On first migration, two default accounts are created:
- **SuperAdmin Account**: username=`admin`, email=`admin@possystem.local`, password=`Admin@123456`
- **Cashier Account**: username=`cashier`, email=`cashier@possystem.local`, password=`Cashier@123456`

## JWT Configuration

### Settings (appsettings.json)
```json
{
  "Jwt": {
	"Secret": "CHANGE_ME_MINIMUM_32_BYTE_LONG_SECRET_KEY",
	"Issuer": "PosSystemApi",
	"Audience": "PosSystemClient",
	"ExpiryMinutes": 15
  }
}
```

**Important:** 
- JWT Secret must be at least 256 bits (32 bytes) for HS256 algorithm
- Change the secret in production to a secure, randomly generated value
- Keep the secret secure and never commit to version control

### Token Claims
JWT tokens contain the following claims:
- `sub` (Subject ID): User ID
- `name`: Username
- `email`: User email
- `role`: User role (Cashier or SuperAdmin)
- `iat` (Issued At): Token creation timestamp
- `exp` (Expiration): Token expiry timestamp

### Token Expiry
- Default: 15 minutes
- Configurable via `Jwt:ExpiryMinutes` in appsettings.json
- After expiry, user must login again to get new token

## Endpoint Authorization

### Authentication Endpoints (`/api/auth`)
| Endpoint | Method | Authorization | Role Restriction |
|----------|--------|---------------|--------------------|
| `/register` | POST | Public (with role check) | SuperAdmin can assign SuperAdmin role |
| `/login` | POST | Public | N/A |
| `/logout` | POST | [Authorize] | Both roles |
| `/profile` | GET | [Authorize] | Both roles |
| `/validate-token` | POST | Public | N/A |

### Catalog Endpoints (`/api/catalog`)
| Endpoint | Method | Authorization | Role Restriction |
|----------|--------|---------------|--------------------|
| `/` | GET | [Authorize] | Both roles |
| `/{id}` | GET | [Authorize] | Both roles |
| `/low-stock` | GET | [Authorize] | Both roles |
| `/sku/{sku}` | GET | [Authorize] | Both roles |
| `/` | POST | [Authorize] | SuperAdmin only |
| `/{id}` | PUT | [Authorize] | SuperAdmin only |
| `/{id}` | DELETE | [Authorize] | SuperAdmin only |

### POS Endpoints (`/api/pos`)
| Endpoint | Method | Authorization | Role Restriction |
|----------|--------|---------------|--------------------|
| `/cart` | GET | [Authorize] | Both roles |
| `/cart/add` | POST | [Authorize] | Both roles |
| `/cart/undo` | POST | [Authorize] | Both roles |
| `/cart/clear` | POST | [Authorize] | Both roles |
| `/checkout` | POST | [Authorize] | Both roles |
| `/daily-summary` | GET | [Authorize] | SuperAdmin only |
| `/shift` | GET | [Authorize] | Both roles |
| `/shift/close` | POST | [Authorize] | SuperAdmin only |

## API Usage Examples

### 1. Register a New User
```bash
curl -X POST "https://localhost:5001/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
	"username": "newuser",
	"email": "user@example.com",
	"password": "SecurePassword123",
	"confirmPassword": "SecurePassword123"
  }'
```

**Response:**
```json
{
  "message": "User 'newuser' registered successfully with role 'Cashier'",
  "user": {
	"id": 3,
	"username": "newuser",
	"email": "user@example.com",
	"role": "Cashier",
	"createdAt": "2026-08-10T12:34:56Z",
	"isActive": true,
	"lastLoginAt": null
  }
}
```

### 2. Login (Get JWT Token)
```bash
curl -X POST "https://localhost:5001/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
	"username": "cashier",
	"password": "Cashier@123456"
  }'
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
	"id": 2,
	"username": "cashier",
	"email": "cashier@possystem.local",
	"role": "Cashier",
	"createdAt": "2026-08-10T10:00:00Z",
	"isActive": true,
	"lastLoginAt": "2026-08-10T12:35:00Z"
  },
  "message": "Authentication successful"
}
```

### 3. Access Protected Endpoint (with JWT)
```bash
curl -X GET "https://localhost:5001/api/catalog" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### 4. Swagger UI Testing
1. Navigate to: `https://localhost:5001/swagger`
2. Click "Authorize" button
3. Enter token: `Bearer {access_token}`
4. Click "Authorize"
5. All protected endpoints are now accessible with the token

## Security Best Practices

### In Production
1. **Change JWT Secret**: Generate a cryptographically secure 32+ byte secret
   ```csharp
   using System.Security.Cryptography;
   var key = new byte[32];
   using (var rng = RandomNumberGenerator.Create())
   {
	   rng.GetBytes(key);
   }
   var secret = Convert.ToBase64String(key);
   ```

2. **Use Environment Variables**: Never hardcode secrets
   ```json
   {
	 "Jwt": {
	   "Secret": "${JWT_SECRET}",
	   "Issuer": "${JWT_ISSUER}",
	   "Audience": "${JWT_AUDIENCE}"
	 }
   }
   ```

3. **HTTPS Only**: Ensure HTTPS is enforced in production
   - `app.UseHttpsRedirection();` in middleware

4. **Token Rotation**: Implement refresh token pattern for longer sessions
   - Short-lived access tokens (15 min)
   - Long-lived refresh tokens (7 days)

5. **Token Blacklist**: Implement token revocation for logout
   - Cache blacklisted tokens
   - Invalidate on password change

6. **Rate Limiting**: Protect auth endpoints from brute force
   - Limit login attempts
   - Implement exponential backoff

7. **Audit Logging**: Log authentication events
   - Failed login attempts
   - Role changes
   - Administrative actions

8. **Password Policy**: Enforce strong passwords
   - Minimum 8 characters
   - Require uppercase, lowercase, numbers, special chars
   - Prevent common passwords

## Troubleshooting

### Issue: 401 Unauthorized on Protected Endpoint
**Cause**: Missing or invalid JWT token

**Solution**:
1. Login first to get token: `POST /api/auth/login`
2. Include token in header: `Authorization: Bearer {token}`
3. Verify token hasn't expired (default 15 minutes)

### Issue: 403 Forbidden on Endpoint
**Cause**: User's role doesn't have permission

**Solution**:
1. Verify user role: `GET /api/auth/profile`
2. Login as SuperAdmin if needed
3. Check endpoint's role requirement in documentation

### Issue: Token Validation Failed
**Cause**: Invalid JWT secret in configuration

**Solution**:
1. Verify JWT:Secret in appsettings.json is ≥256 bits
2. Ensure Issuer and Audience match configuration
3. Check token hasn't been tampered with

### Issue: Cannot Create SuperAdmin User
**Cause**: Only SuperAdmin can create SuperAdmin accounts

**Solution**:
1. Login as existing SuperAdmin first
2. Add role="SuperAdmin" to RegisterDto
3. Or let the user register as Cashier and promote manually

## Database Migration

To apply the RBAC schema changes to an existing database:

```bash
# Create migration
dotnet ef migrations add AddRoleBasedAccessControl

# Apply migration
dotnet ef database update
```

This will:
1. Add `Role` column to Users table
2. Add `LastLoginAt` column to Users table
3. Create index on Role column
4. Seed default SuperAdmin and Cashier accounts

## References

- [JWT (JSON Web Tokens)](https://jwt.io/)
- [RFC 7519 - JWT Specification](https://tools.ietf.org/html/rfc7519)
- [ASP.NET Core JWT Authentication](https://docs.microsoft.com/en-us/aspnet/core/security/authentication/jwt)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [BCrypt Password Hashing](https://en.wikipedia.org/wiki/Bcrypt)

## Support

For issues or questions regarding RBAC implementation:
1. Check logs in `/bin/Debug/net8.0/` directory
2. Enable debug logging in appsettings.Development.json
3. Test endpoints in Swagger UI at `/swagger`
4. Review token claims using https://jwt.io
