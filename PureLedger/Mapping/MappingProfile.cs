using AutoMapper;
using PureLedger.Models;
using PureLedger.ViewModels;
using PureLedger.ViewModels.Home;
using PureLedger.ViewModels.Reporting;
using PureLedger.ViewModels.DeliveryChallan;

namespace PureLedger.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mappings
            CreateMap<User, UserViewModel>()
                .ForMember(dest => dest.Password, opt => opt.Ignore())
                .ForMember(dest => dest.ConfirmPassword, opt => opt.Ignore())
                .ForMember(dest => dest.ProjectIds, opt => opt.MapFrom(src => src.UserProjects.Select(up => up.ProjectId).ToList()));

            CreateMap<UserViewModel, User>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.UserProjects, opt => opt.Ignore())
                .ForMember(dest => dest.ControllerAccess, opt => opt.Ignore());

            // Project mappings
            CreateMap<Project, ProjectViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ProjectId.ToString()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.ProjectName));

            // Category mappings
            CreateMap<Category, CategoryViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.CategoryId.ToString()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.CategoryName));

            // Account mappings
            CreateMap<Account, AccountViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.AccountId.ToString()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.AccountName));

            // Location mappings
            CreateMap<Location, LocationViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.LocationId.ToString()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.LocationName));

            // Booker mappings
            CreateMap<Booker, BookerViewModel>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.BookerID.ToString()))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.BookerName));

            // Journal Entry mappings
            CreateMap<JournalEntry, JournalEntryRowModel>();
            CreateMap<JournalEntryRowModel, JournalEntry>();

            // Journal Voucher mappings
            CreateMap<JournalVoucher, JournalVoucherReportViewModel>();

            // Delivery Challan mappings
            CreateMap<DeliveryChallanVoucher, DeliveryChallanViewModels>();
            CreateMap<DeliveryChallanEntry, DeliveryChallanEntryViewModel>();
        }
    }

    // Helper ViewModels for mapping
    public class AccountViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class BookerViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class DeliveryChallanEntryViewModel
    {
        public int EntryId { get; set; }
        public int JournalEntryId { get; set; }
        public DateOnly DCDate { get; set; }
        public string? OtherReference { get; set; }
        public int? LocationId { get; set; }
        public string? Remarks { get; set; }
    }
}

