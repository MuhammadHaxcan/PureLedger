# Delivery Challan Location Editing Feature

## Summary
Added functionality to show and edit delivery challan location when editing journal entries in the VoucherEntry page.

## Changes Made

### Backend Changes

#### 1. Repository Layer
- **File**: `Repositories/Interfaces/IDeliveryChallanEntryRepository.cs`
  - Added method: `GetByJournalEntryIdAsync(int journalEntryId)` - Get delivery challan entry by journal entry ID only

- **File**: `Repositories/DeliveryChallanEntryRepository.cs`
  - Implemented `GetByJournalEntryIdAsync` method with Location navigation property included

#### 2. Service Layer
- **File**: `Services/JournalEntryService.cs`
  - Added public method: `GetDeliveryChallanEntryByJournalEntryIdAsync(int journalEntryId)`
  - Updated `GetJournalEntryForEditAsync` to check for delivery challan entries regardless of account group (not just AccountGroup 2)
  - Updated `UpdateJournalEntryAsync` to handle location updates for any entry with delivery challan (removed AccountGroup 2 restriction)

#### 3. Controller Layer
- **File**: `Controllers/Api/HomeApiController.cs`
  - Updated `GetVoucherById` endpoint to include delivery challan location information for each entry:
    - `hasDeliveryChallan` - boolean indicating if entry has delivery challan
    - `locationId` - location ID if exists
    - `locationName` - location name if exists
    - `deliveryChallanEntryId` - delivery challan entry ID if exists

### Frontend Changes

#### 1. VoucherEntry Component
- **File**: `Pureledger-Frontend/src/pages/Home/VoucherEntry.jsx`
  - Added Location column to the voucher entries table
  - Updated `handleEditEntry` to:
    - Check for delivery challan entry from both API response and entry object
    - Show location field when delivery challan exists
    - Load all locations when delivery challan exists
  - Added `loadEditLocations` function to fetch locations from MetaDataApi
  - Updated edit form submission to include `deliveryChallanEntryId`
  - Location dropdown now shows when editing entries with delivery challan

## API Endpoints Used

### Existing Endpoints (No Changes Needed)
- `GET /api/metadata/locations` - Get all locations (used in frontend)
- `GET /api/home/journal-entry/{entryId}` - Get journal entry for editing (returns location info)
- `PUT /api/home/journal-entry` - Update journal entry (handles location updates)

### Updated Endpoints
- `GET /api/home/voucher/{id}` - Now returns location information for entries with delivery challan

## How It Works

1. **Viewing Voucher**: When viewing a voucher, entries with delivery challan show their location in the Location column
2. **Editing Entry**: 
   - When clicking Edit on an entry with delivery challan, the edit modal shows a Location dropdown
   - All locations are loaded and displayed
   - Current location is pre-selected
   - User can change the location
3. **Saving Changes**: When saving, the location is updated in the delivery challan entry

## Testing Checklist

- [ ] View voucher with entries that have delivery challan - location should be visible
- [ ] Edit entry with delivery challan - location dropdown should appear
- [ ] Change location in edit modal - location should update
- [ ] Edit entry without delivery challan - location field should not appear
- [ ] Verify location updates are persisted in database

