using AD.Recruitment.Application.Enums;
using AD.Recruitment.Core.Dtos;
using AD.Recruitment.Core.Entities;
using AD.Recruitment.Core.Entities.Identity;
using AD.Recruitment.Core.Interfaces;
using AD.Recruitment.Infrastracture.Data;

using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AD.Recruitment.Application
{

    public class FulfilmentService : IFulfilmentService
    {
        private readonly ILogger<CustomerService> _logger;
        private readonly IMapper _mapper;
        private readonly RecruitmentContext _recruitmentContext;
        public FulfilmentService(IMapper mapper, ILogger<CustomerService> logger, RecruitmentContext recruitmentContext)
        {
            _logger = logger;
            _mapper = mapper;
            _recruitmentContext = recruitmentContext;
        }

        public async Task<bool> FeedbackExists(FulfilmentStatusUpdateDto fulfilmentStatusUpdateDto)
        {
            var exists = await _recruitmentContext.PanelFeedbackXrefs
                .AnyAsync(p => p.FulfilmentId == fulfilmentStatusUpdateDto.FulfilmentId && p.InterviewLevelId == fulfilmentStatusUpdateDto.InterviewLevelId);

            return exists;
        }


        public async Task<List<FulfilmentCandidateDto>> GetCandidatesForFulfilment(FulfilmentCandidateSearchDto searchDto)
        {
            List<FulfilmentCandidateDto> fulfilmentCandidateDtos = new List<FulfilmentCandidateDto>();
            try
            {
                List<Candidate> candidateList = null;

                if (searchDto.Skills != null && searchDto.Skills.Count > 0)
                {
                    candidateList = await _recruitmentContext.CandidateSkillXrefs.Where(c => searchDto.Skills.Any(s => s == c.SkillId))
                     //.Include(d=>d.Candidate).ThenInclude(c=>c.Recruiter)
                     .Include(d => d.Candidate).ThenInclude(c => c.Fulfilments).ThenInclude(f => f.Requirement)
                     .Include(d => d.Candidate).ThenInclude(c => c.Fulfilments).ThenInclude(f => f.FulfilmentStatus)
                     .Include(d => d.Candidate).ThenInclude(f => f.CandidateSkillsXrefs).ThenInclude(x => x.Skill)
                     .Include(s => s.Skill)
                     .Select(s => s.Candidate)
                     .Where(c => string.IsNullOrEmpty(searchDto.SearchTerm) ||
                      c.EmailId == searchDto.SearchTerm ||
                      c.FirstName == searchDto.SearchTerm ||
                      c.PhoneNumber == searchDto.SearchTerm).Distinct().ToListAsync();
                }
                else if (!string.IsNullOrEmpty(searchDto.SearchTerm))
                {
                    candidateList = await _recruitmentContext.CandidateSkillXrefs.Where(c =>
                      c.Candidate.EmailId.Contains(searchDto.SearchTerm) ||
                      c.Candidate.FirstName.Contains(searchDto.SearchTerm) ||
                      c.Candidate.PhoneNumber.Contains(searchDto.SearchTerm))
                     .Include(d => d.Candidate).ThenInclude(c => c.Fulfilments).ThenInclude(f => f.Requirement)
                     .Include(d => d.Candidate).ThenInclude(c => c.Fulfilments).ThenInclude(f => f.FulfilmentStatus)
                     .Include(d => d.Candidate).ThenInclude(f => f.CandidateSkillsXrefs).ThenInclude(x => x.Skill)
                     .Include(s => s.Skill)
                     .Select(s => s.Candidate).
                     Distinct().ToListAsync();
                }
                else
                {
                    candidateList = new List<Candidate>();
                }

                if (candidateList.Any())
                {
                    foreach (var candidate in candidateList)
                    {
                        bool isAddedToTheSameRequirement = false;
                        var fulfilement = new FulfilmentCandidateDto
                        {
                            CandidateId = candidate.Id,
                            Email = candidate.EmailId,
                            CurrentCTC = candidate.CTC,
                            ExpectedCTC = candidate.ECTC,
                            FirstName = candidate.FirstName,
                            LastName = candidate.LastName,
                            PhoneNumber = candidate.PhoneNumber,
                            //  Recruiter = candidate.Recruiter.DisplayName
                        };
                        if (candidate.Fulfilments.Any())
                        {
                            if (candidate.Fulfilments.Any(d => d.RequirementID == searchDto.RequirementId))
                            {
                                isAddedToTheSameRequirement = true;
                            }
                            var fulfilment = candidate.Fulfilments.FirstOrDefault();
                            if (fulfilment != null)
                            {
                                fulfilement.FulfilmentDetails = $"{fulfilment.Requirement.RequirementId} - {fulfilment.FulfilmentStatus.Name}";
                            }
                        }
                        fulfilement.PrimarySkills = string.Join(",", candidate.CandidateSkillsXrefs.Where(c => c.IsDeleted == false && c.SkillTypeId == (int)ESkillType.Primary).Select(s => s.Skill.Name).ToList());
                        if (!isAddedToTheSameRequirement)
                        {
                            fulfilmentCandidateDtos.Add(fulfilement);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string s = "";
            }
            return fulfilmentCandidateDtos;
        }

        public async Task<List<FulfilmentViewDto>> GetFulfilmentsByRequirement(int requirementId)
        {
            List<FulfilmentViewDto> fulfilmentViewDtos = new List<FulfilmentViewDto>();

            try
            {
                var users = await _recruitmentContext.Users.ToListAsync();
                var fulfilments = await _recruitmentContext.Fulfilments.Where(d => d.RequirementID == requirementId)
                    .Include(s => s.InterviewLevel)
                    .Include(d => d.FulfilmentStatus)
                    //.Include(d => d.Candidate).ThenInclude(d => d.Recruiter)
                    .Include(d => d.Candidate).ThenInclude(c => c.CandidateSkillsXrefs).ThenInclude(c => c.Skill).ToListAsync();

                if (fulfilments.Any())
                {
                    foreach (var fulfilment in fulfilments)
                    {
                        FulfilmentViewDto fulfilmentViewDto = new FulfilmentViewDto();

                        fulfilmentViewDto.CandidateId = fulfilment.CandidateId;
                        fulfilmentViewDto.InterviewLevelId = fulfilment.InterviewLevelId;
                        fulfilmentViewDto.CurrentCTC = fulfilment.Candidate.CTC;
                        fulfilmentViewDto.Email = fulfilment.Candidate.EmailId;
                        fulfilmentViewDto.FirstName = fulfilment.Candidate.FirstName;
                        fulfilmentViewDto.LastName = fulfilment.Candidate.LastName;
                        fulfilmentViewDto.ExpectedCTC = fulfilment.Candidate.ECTC;
                        fulfilmentViewDto.Id = fulfilment.Id;
                        fulfilmentViewDto.StatusId = fulfilment.FulfilmentStatusId;
                        fulfilmentViewDto.Status = fulfilment.FulfilmentStatus.Name;
                        fulfilmentViewDto.InterviewLevelName = fulfilment.InterviewLevel.Name;
                        fulfilmentViewDto.PhoneNumber = fulfilment.Candidate.PhoneNumber;
                        fulfilmentViewDto.ReasonId = fulfilment.ReasonId;
                        fulfilmentViewDto.ReasonRemarks = fulfilment.ReasonRemarks;
                        fulfilmentViewDto.PanelId = fulfilment.PanelId;
                        fulfilmentViewDto.InterviewDateTime = fulfilment.InterviewDateTime;
                        var user = users.FirstOrDefault(u => u.Id == fulfilment.CreatedBy);
                        if (user != null)
                            fulfilmentViewDto.Recruiter = user.DisplayName;

                        fulfilmentViewDto.PrimarySkills = string.Join(",", fulfilment.Candidate.CandidateSkillsXrefs.Where(c => c.SkillTypeId == (int)ESkillType.Primary).Select(s => s.Skill.Name).ToList());
                        fulfilmentViewDtos.Add(fulfilmentViewDto);

                    }
                }
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
            return fulfilmentViewDtos;
        }

        public async Task<string> SavePanelFeedback(PanelFeedbackXrefDto panelFeedbackXrefDto)
        {
            string result = string.Empty;
            var existingFeedback = await _recruitmentContext.PanelFeedbackXrefs
                .FirstOrDefaultAsync(x => x.FulfilmentId == panelFeedbackXrefDto.FulfilmentId &&
                                            x.InterviewLevelId == panelFeedbackXrefDto.InterviewLevelId &&
                                            x.SkillId == panelFeedbackXrefDto.SkillId);

            if (existingFeedback != null)
            {
                result = "Feedback already exists";
                return result;
            }
            var panelFeedbackXref = new PanelFeedbackXref
            {
                FulfilmentId = panelFeedbackXrefDto.FulfilmentId,
                InterviewLevelId = panelFeedbackXrefDto.InterviewLevelId,
                SkillId = panelFeedbackXrefDto.SkillId,
                Ratting = panelFeedbackXrefDto.Ratting,
                Comment = panelFeedbackXrefDto.Comment
            };

            _recruitmentContext.PanelFeedbackXrefs.Add(panelFeedbackXref);
            await _recruitmentContext.SaveChangesAsync();
            result = "Submitted successfully";
            return result;
        }




        public async Task<string> SubmitCandidateForFulfilment(SubmitCandidateForFulfilmentDto submitCandidateForFulfilment)
        {
            string result = string.Empty;

            Fulfilment fulfilment = await _recruitmentContext.Fulfilments.FirstOrDefaultAsync(d => d.RequirementID == submitCandidateForFulfilment.RequirementId && d.CandidateId == submitCandidateForFulfilment.CandidateId);

            if (fulfilment == null)
            {
                fulfilment = new Fulfilment();
                fulfilment.RequirementID = submitCandidateForFulfilment.RequirementId;
                fulfilment.CandidateId = submitCandidateForFulfilment.CandidateId;
                fulfilment.FulfilmentStatusId = (int)EFulfilmentStatus.InProgress;
                fulfilment.InterviewLevelId = 1;

                _recruitmentContext.Fulfilments.Add(fulfilment);
                await _recruitmentContext.SaveChangesAsync();

                result = "Success";
            }
            else
            {
                result = "Candidate already submitted for the given Requirement";

            }
            return result;
        }

        public async Task<AddRequirementDto> GetPanelId(int id)
        {
            AddRequirementDto addRequirementDto;
            _logger.LogInformation($"GetRequirementById : {id}");
            try
            {
                var requirementData = await _recruitmentContext.Requirements.Where(d => d.Id == id)
                                            .Include(d => d.RequirementPanelXrefs)
                                            //.Include(s => s.RequirementPanelXrefs)
                                           .FirstOrDefaultAsync();


                // Map AddRequirementDto to Requirement model 
                addRequirementDto = _mapper.Map<Requirement, AddRequirementDto>(requirementData);
                addRequirementDto.PanelId = requirementData.RequirementPanelXrefs?.Where(d => d.IsDeleted == false).Select(d => d.PanelId).ToList();

 

            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while GetRequirementById, Exception message :{ex.Message},Inner Exception :{ex.InnerException?.Message}, ID : {id}  ");
                throw;
            }
            return addRequirementDto;

        }

        public async Task<string> UpdateFulfilmentStatus(FulfilmentStatusUpdateDto fulfilmentStatusUpdateDto)
        {
            string result = "Update failed";

            var fulfilment = await _recruitmentContext.Fulfilments
                .Include(f => f.Requirement)
                .FirstOrDefaultAsync(f => f.Id == fulfilmentStatusUpdateDto.FulfilmentId);

            if (fulfilment == null)
            {
                return "Fulfilment not found";
            }
            var requirement = fulfilment.Requirement;
            int joinedCount = await _recruitmentContext.Fulfilments
                .Where(f => f.RequirementID == requirement.Id && f.FulfilmentStatusId == fulfilmentStatusUpdateDto.StatusId)
                .CountAsync();
            if (fulfilmentStatusUpdateDto.StatusId == (int)EFulfilmentStatus.Joined)
            {
                if (joinedCount >= requirement.NumberOfVacancies)
                {
                    return "Joined candidates must not exceed available vacancies.";
                }
            }
            fulfilment.FulfilmentStatusId = fulfilmentStatusUpdateDto.StatusId;
            fulfilment.InterviewLevelId = fulfilmentStatusUpdateDto.InterviewLevelId;
            fulfilment.ReasonId = fulfilmentStatusUpdateDto.ReasonCodeId;
            fulfilment.ReasonRemarks = fulfilmentStatusUpdateDto.ReasonRemarks;
            fulfilment.PanelId = fulfilmentStatusUpdateDto.PanelId;
            fulfilment.InterviewDateTime = fulfilmentStatusUpdateDto.InterviewDateTime;
            fulfilment.InterviewMeetingLink = fulfilmentStatusUpdateDto.InterviewMeetingLink;

            _recruitmentContext.Fulfilments.Update(fulfilment);
            await _recruitmentContext.SaveChangesAsync();
            result = "Updated successfully";

            return result;
        }

       
    }
}
