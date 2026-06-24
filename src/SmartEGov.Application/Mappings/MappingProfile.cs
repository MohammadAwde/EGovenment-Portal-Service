using AutoMapper;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Citizen <-> CitizenDto
        CreateMap<Citizen, CitizenDto>();
        CreateMap<CitizenDto, Citizen>()
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.ServiceRequests, opt => opt.Ignore());

        // ServiceRequest -> ServiceRequestDto
        CreateMap<ServiceRequest, ServiceRequestDto>()
            .ForMember(dest => dest.GovernmentServiceName, opt => opt.MapFrom(src => src.GovernmentService != null ? src.GovernmentService.Name : null))
            .ForMember(dest => dest.CitizenName, opt => opt.MapFrom(src => src.Citizen != null ? src.Citizen.FirstName + " " + src.Citizen.LastName : null));

        // ApprovalStep -> ApprovalStepDto
        CreateMap<ApprovalStep, ApprovalStepDto>()
            .ForMember(dest => dest.OfficerName, opt => opt.MapFrom(src => src.Officer != null ? src.Officer.FullName : null));

        // Document -> DocumentDto
        CreateMap<Document, DocumentDto>();

        // RequiredDocument -> RequiredDocumentDto
        CreateMap<RequiredDocument, RequiredDocumentDto>();

        // GovernmentService -> GovernmentServiceDto
        CreateMap<GovernmentService, GovernmentServiceDto>()
            .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : null))
            .ForMember(dest => dest.RequiredDocuments, opt => opt.MapFrom(src => src.RequiredDocuments));

        // Notification -> NotificationDto
        CreateMap<Notification, NotificationDto>();

        // AuditLog -> AuditLogDto
        CreateMap<AuditLog, AuditLogDto>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : null));

        // Payment -> PaymentDto
        CreateMap<Payment, PaymentDto>()
            .ForMember(dest => dest.ServiceRequestReference, opt => opt.MapFrom(src => src.ServiceRequest != null ? src.ServiceRequest.ReferenceNumber : null))
            .ForMember(dest => dest.GovernmentServiceName, opt => opt.MapFrom(src => src.ServiceRequest != null && src.ServiceRequest.GovernmentService != null ? src.ServiceRequest.GovernmentService.Name : null));
        CreateMap<ServiceCenter, ServiceCenterDto>();

        CreateMap<Appointment, AppointmentDto>()
            .ForMember(d => d.ServiceCenterName,
                o => o.MapFrom(s => s.ServiceCenter != null ? s.ServiceCenter.CenterName : null))
            .ForMember(d => d.ServiceCenterAddress,
                o => o.MapFrom(s => s.ServiceCenter != null ? s.ServiceCenter.Address : null))
            .ForMember(d => d.GovernmentServiceName,
                o => o.MapFrom(s => s.GovernmentService != null ? s.GovernmentService.Name : null));

    }
}
