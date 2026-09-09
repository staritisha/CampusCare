using CampusCare.Core.DTOs;
using CampusCare.Core.Entities;
using CampusCare.Core.Enums;
using CampusCare.Core.Interfaces;
using CampusCare.Infrastructure.Data;
using CampusCare.WebAPI.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CampusCare.Tests.Unit
{
    public class WebAPITests
    {
        private readonly Mock<IComplaintRepository> _repoMock;
        private readonly Mock<IAIService> _aiServiceMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<IEscalationService> _escalationMock;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<RoleManager<IdentityRole>> _roleManagerMock;
        private readonly ApplicationDbContext _dbContext;

        public WebAPITests()
        {
            _repoMock = new Mock<IComplaintRepository>();
            _aiServiceMock = new Mock<IAIService>();
            _notificationMock = new Mock<INotificationService>();
            _escalationMock = new Mock<IEscalationService>();

            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(roleStoreMock.Object, null!, null!, null!, null!);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _dbContext = new ApplicationDbContext(options);
        }

        private ComplaintsApiController CreateComplaintsController()
        {
            return new ComplaintsApiController(
                _repoMock.Object,
                _aiServiceMock.Object,
                _notificationMock.Object,
                _escalationMock.Object,
                _userManagerMock.Object,
                _dbContext);
        }

        #region ComplaintsApiController Tests

        [Fact]
        public async Task GetComplaints_ShouldReturnOkResult_WithListOfComplaints()
        {
            // Arrange
            var testComplaints = new List<Complaint>
            {
                new Complaint
                {
                    Id = 1,
                    ComplaintNumber = "CMP-2026-00001",
                    Title = "Wi-Fi Issue in Lab 1",
                    Description = "Internet connection dropped",
                    Location = "Lab 1",
                    Status = ComplaintStatus.Submitted,
                    Priority = PriorityLevel.High,
                    CreatedAt = DateTime.UtcNow
                },
                new Complaint
                {
                    Id = 2,
                    ComplaintNumber = "CMP-2026-00002",
                    Title = "Water Pipe Leak in Hostel A",
                    Description = "Pipe leaking near room 102",
                    Location = "Hostel A",
                    Status = ComplaintStatus.InProgress,
                    Priority = PriorityLevel.Medium,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(testComplaints);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.GetComplaints(new ComplaintQueryFilter());

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            var items = ((IEnumerable<Complaint>)okResult.Value).ToList();
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public async Task GetComplaintDetails_ShouldReturnOkResult_WhenComplaintExists()
        {
            // Arrange
            var complaint = new Complaint
            {
                Id = 1,
                ComplaintNumber = "CMP-2026-00001",
                Title = "Library Air Conditioner Defective",
                Description = "A/C unit making loud noise",
                Location = "Central Library 2nd Floor",
                Status = ComplaintStatus.Assigned,
                Priority = PriorityLevel.Medium,
                CreatedAt = DateTime.UtcNow,
                History = new List<ComplaintHistory>()
            };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.GetComplaintDetails(1);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task GetComplaintDetails_ShouldReturnNotFound_WhenComplaintDoesNotExist()
        {
            // Arrange
            _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Complaint?)null);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.GetComplaintDetails(999);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task CreateComplaint_ShouldReturnCreatedAtAction_WhenValid()
        {
            // Arrange
            _aiServiceMock.Setup(a => a.AnalyzeComplaintAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new AIAnalysisResult
                {
                    Category = "Wi-Fi",
                    Department = "Information Technology",
                    Priority = PriorityLevel.High,
                    Summary = "Wi-Fi issue summary",
                    ModelUsed = "RuleEngine",
                    IsSuccess = true
                });

            _repoMock.Setup(r => r.GenerateUniqueComplaintNumberAsync()).ReturnsAsync("CMP-2026-00001");
            _repoMock.Setup(r => r.AddAsync(It.IsAny<Complaint>())).Returns(Task.CompletedTask);
            _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(new Complaint
            {
                Id = 1,
                ComplaintNumber = "CMP-2026-00001",
                Title = "Network connection down",
                Status = ComplaintStatus.Submitted
            });

            var controller = CreateComplaintsController();

            var dto = new CreateComplaintDto
            {
                Title = "Network connection down",
                Description = "Network is down across lab 4",
                Location = "Lab 4",
                StudentId = "student-1",
                StudentEmail = "student1@college.com"
            };

            // Act
            var result = await controller.CreateComplaint(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.NotNull(createdResult.Value);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<Complaint>()), Times.Once);
        }

        [Fact]
        public async Task UpdateComplaintStatus_ShouldReturnOkResult_OnValidTransition()
        {
            // Arrange
            var complaint = new Complaint
            {
                Id = 1,
                ComplaintNumber = "CMP-2026-00001",
                Status = ComplaintStatus.Assigned,
                StudentId = "student-1"
            };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Complaint>())).Returns(Task.CompletedTask);

            var controller = CreateComplaintsController();

            var dto = new UpdateComplaintStatusDto
            {
                ComplaintId = 1,
                NewStatus = ComplaintStatus.InProgress,
                UserId = "staff-1",
                CommentText = "Started investigation."
            };

            // Act
            var result = await controller.UpdateComplaintStatus(1, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal(ComplaintStatus.InProgress, complaint.Status);
        }

        [Fact]
        public async Task UpdateComplaintStatus_ShouldReturnBadRequest_OnInvalidTransition()
        {
            // Arrange
            var complaint = new Complaint
            {
                Id = 1,
                ComplaintNumber = "CMP-2026-00001",
                Status = ComplaintStatus.Submitted
            };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);

            var controller = CreateComplaintsController();

            var dto = new UpdateComplaintStatusDto
            {
                ComplaintId = 1,
                NewStatus = ComplaintStatus.Closed, // Invalid transition from Submitted to Closed directly
                UserId = "staff-1"
            };

            // Act
            var result = await controller.UpdateComplaintStatus(1, dto);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task AssignStaff_ShouldAssignTechnician_WhenStaffExists()
        {
            // Arrange
            var complaint = new Complaint
            {
                Id = 1,
                ComplaintNumber = "CMP-2026-00001",
                Status = ComplaintStatus.Submitted,
                StudentId = "student-1"
            };

            var staffUser = new ApplicationUser { Id = "staff-1", FullName = "John Staff", Email = "staff@college.com" };

            _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(complaint);
            _userManagerMock.Setup(u => u.FindByIdAsync("staff-1")).ReturnsAsync(staffUser);
            _repoMock.Setup(r => r.UpdateAsync(It.IsAny<Complaint>())).Returns(Task.CompletedTask);

            var controller = CreateComplaintsController();

            var dto = new AssignComplaintDto
            {
                ComplaintId = 1,
                SelectedStaffId = "staff-1",
                ManagerId = "mgr-1",
                Priority = PriorityLevel.High
            };

            // Act
            var result = await controller.AssignStaff(1, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            Assert.Equal("staff-1", complaint.AssignedStaffId);
            Assert.Equal(ComplaintStatus.Assigned, complaint.Status);
        }

        [Fact]
        public async Task TriggerEscalationCheck_ShouldReturnOkResult_WithEscalatedCount()
        {
            // Arrange
            _escalationMock.Setup(e => e.ProcessOverdueComplaintsAsync(48)).ReturnsAsync(3);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.TriggerEscalationCheck(48);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            _escalationMock.Verify(e => e.ProcessOverdueComplaintsAsync(48), Times.Once);
        }

        [Fact]
        public async Task DeleteComplaint_ShouldReturnOkResult_WhenComplaintExists()
        {
            // Arrange
            var complaint = new Complaint
            {
                Id = 5,
                ComplaintNumber = "CMP-2026-00005",
                Title = "Broken Desk",
                Description = "Leg broken on desk",
                Location = "Room 304"
            };

            _repoMock.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(complaint);
            _repoMock.Setup(r => r.DeleteAsync(5)).Returns(Task.CompletedTask);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.DeleteComplaint(5);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            _repoMock.Verify(r => r.DeleteAsync(5), Times.Once);
        }

        [Fact]
        public async Task DeleteComplaint_ShouldReturnNotFound_WhenComplaintDoesNotExist()
        {
            // Arrange
            _repoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Complaint?)null);

            var controller = CreateComplaintsController();

            // Act
            var result = await controller.DeleteComplaint(999);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public void ReceiveN8nWebhookCallback_ShouldReturnOkResult()
        {
            // Arrange
            var controller = CreateComplaintsController();
            var payload = new { Event = "ComplaintEscalated", ComplaintId = 10 };

            // Act
            var result = controller.ReceiveN8nWebhookCallback(payload);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        #endregion

        #region DepartmentsApiController Tests

        [Fact]
        public async Task DepartmentsApi_CreateAndGet_ShouldSucceed()
        {
            // Arrange
            var controller = new DepartmentsApiController(_dbContext, _userManagerMock.Object);

            var createDto = new CreateDepartmentDto
            {
                Name = "Health & Medical",
                Code = "MED",
                Description = "Campus health center"
            };

            // Act
            var createResult = await controller.CreateDepartment(createDto);
            var getResult = await controller.GetDepartments();

            // Assert
            var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
            Assert.NotNull(createdAction.Value);

            var okGet = Assert.IsType<OkObjectResult>(getResult);
            var list = ((IEnumerable<Department>)okGet.Value!).ToList();
            Assert.Contains(list, d => d.Code == "MED");
        }

        #endregion

        #region CategoriesApiController Tests

        [Fact]
        public async Task CategoriesApi_CreateAndGet_ShouldSucceed()
        {
            // Arrange
            var dept = new Department { Id = 99, Name = "Lab Facilities", Code = "LAB" };
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();

            var controller = new CategoriesApiController(_dbContext);

            var createDto = new CreateCategoryDto
            {
                Name = "Chemistry Equipment",
                Description = "Beakers and burners",
                DefaultDepartmentId = dept.Id
            };

            // Act
            var createResult = await controller.CreateCategory(createDto);
            var getResult = await controller.GetCategories();

            // Assert
            var createdAction = Assert.IsType<CreatedAtActionResult>(createResult);
            Assert.NotNull(createdAction.Value);

            var okGet = Assert.IsType<OkObjectResult>(getResult);
            var list = ((IEnumerable<ComplaintCategory>)okGet.Value!).ToList();
            Assert.Contains(list, c => c.Name == "Chemistry Equipment");
        }

        #endregion

        #region UsersApiController Tests

        [Fact]
        public async Task UsersApi_CreateStaff_ShouldSucceed_WhenValid()
        {
            // Arrange
            _userManagerMock.Setup(u => u.FindByEmailAsync("newstaff@college.com")).ReturnsAsync((ApplicationUser?)null);
            _userManagerMock.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);
            _roleManagerMock.Setup(r => r.RoleExistsAsync("Staff")).ReturnsAsync(true);
            _userManagerMock.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Staff"))
                .ReturnsAsync(IdentityResult.Success);

            var controller = new UsersApiController(_userManagerMock.Object, _roleManagerMock.Object, _dbContext);

            var dto = new CreateStaffUserDto
            {
                FullName = "New Staff Member",
                Email = "newstaff@college.com",
                Password = "Password123!",
                DepartmentId = 1,
                Role = "Staff"
            };

            // Act
            var result = await controller.CreateStaff(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
            _userManagerMock.Verify(u => u.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"), Times.Once);
        }

        [Fact]
        public async Task UsersApi_ToggleStatus_ShouldToggleActive()
        {
            // Arrange
            var user = new ApplicationUser { Id = "usr-1", Email = "staff@college.com", IsActive = true };
            _userManagerMock.Setup(u => u.FindByIdAsync("usr-1")).ReturnsAsync(user);
            _userManagerMock.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

            var controller = new UsersApiController(_userManagerMock.Object, _roleManagerMock.Object, _dbContext);

            // Act
            var result = await controller.ToggleUserStatus("usr-1");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.False(user.IsActive);
        }

        #endregion

        #region AnalyticsApiController Tests

        [Fact]
        public async Task AnalyticsApi_AdminAndStudent_ShouldReturnCorrectKPIs()
        {
            // Arrange
            var testComplaints = new List<Complaint>
            {
                new Complaint { Id = 1, Status = ComplaintStatus.Submitted, StudentId = "stu-1", CreatedAt = DateTime.UtcNow },
                new Complaint { Id = 2, Status = ComplaintStatus.Resolved, StudentId = "stu-1", CreatedAt = DateTime.UtcNow, ResolvedAt = DateTime.UtcNow.AddHours(2) }
            };

            _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(testComplaints);
            _repoMock.Setup(r => r.GetByStudentIdAsync("stu-1")).ReturnsAsync(testComplaints);

            var controller = new AnalyticsApiController(_repoMock.Object, _userManagerMock.Object, _dbContext);

            // Act
            var adminResult = await controller.GetAdminAnalytics();
            var studentResult = await controller.GetStudentAnalytics("stu-1");

            // Assert
            var adminOk = Assert.IsType<OkObjectResult>(adminResult);
            var adminDto = Assert.IsType<AdminAnalyticsDto>(adminOk.Value);
            Assert.Equal(2, adminDto.TotalComplaints);
            Assert.Equal(1, adminDto.PendingComplaints);
            Assert.Equal(1, adminDto.ResolvedComplaints);

            var studentOk = Assert.IsType<OkObjectResult>(studentResult);
            var studentDto = Assert.IsType<StudentAnalyticsDto>(studentOk.Value);
            Assert.Equal(2, studentDto.TotalComplaints);
            Assert.Equal(1, studentDto.ResolvedComplaints);
        }

        [Fact]
        public async Task AnalyticsApi_Manager_ShouldReturnDepartmentMetrics()
        {
            // Arrange
            var dept = new Department { Id = 10, Name = "Security & Patrol", Code = "SEC" };
            _dbContext.Departments.Add(dept);
            await _dbContext.SaveChangesAsync();

            var testComplaints = new List<Complaint>
            {
                new Complaint { Id = 101, Status = ComplaintStatus.Assigned, DepartmentId = 10, AssignedStaffId = "staff-sec-1", CreatedAt = DateTime.UtcNow },
                new Complaint { Id = 102, Status = ComplaintStatus.InProgress, DepartmentId = 10, AssignedStaffId = null, CreatedAt = DateTime.UtcNow }
            };

            _repoMock.Setup(r => r.GetByDepartmentIdAsync(10)).ReturnsAsync(testComplaints);

            var controller = new AnalyticsApiController(_repoMock.Object, _userManagerMock.Object, _dbContext);

            // Act
            var result = await controller.GetManagerAnalytics(10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var managerDto = Assert.IsType<ManagerAnalyticsDto>(okResult.Value);
            Assert.Equal(2, managerDto.TotalDepartmentComplaints);
            Assert.Equal(1, managerDto.UnassignedCount);
            Assert.Equal(1, managerDto.InProgressCount);
        }

        #endregion
    }
}
