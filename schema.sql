CREATE TABLE Staff(StaffId int IDENTITY PRIMARY KEY, Login nvarchar(100) NOT NULL UNIQUE, PasswordHash nvarchar(250) NOT NULL, Role nvarchar(20) NOT NULL CHECK(Role IN('Employee','Manager','HR')), ManagerId int NULL REFERENCES Staff);
CREATE TABLE Attendance(StaffId int NOT NULL REFERENCES Staff, WorkDate date NOT NULL, CheckInUtc datetime2 NOT NULL, CheckOutUtc datetime2 NULL, PRIMARY KEY(StaffId,WorkDate));
CREATE TABLE LeaveRequest(LeaveId int IDENTITY PRIMARY KEY,StaffId int NOT NULL REFERENCES Staff,StartDate date NOT NULL,EndDate date NOT NULL,Reason nvarchar(250) NOT NULL,State nvarchar(20) NOT NULL DEFAULT 'Pending Manager',CHECK(EndDate>=StartDate),CHECK(State IN('Pending Manager','Pending HR','Approved','Rejected')));
CREATE INDEX IX_Leave_Staff_Dates ON LeaveRequest(StaffId,StartDate,EndDate) INCLUDE(State);
GO
CREATE PROCEDURE dbo.MarkAttendance @StaffId int,@CheckIn bit AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @now datetime2=SYSUTCDATETIME(),@day date=CONVERT(date,SYSUTCDATETIME());
 IF @CheckIn=1
   INSERT Attendance(StaffId,WorkDate,CheckInUtc) VALUES(@StaffId,@day,@now);
 ELSE
 BEGIN
   UPDATE Attendance SET CheckOutUtc=@now WHERE StaffId=@StaffId AND WorkDate=@day AND CheckOutUtc IS NULL;
   IF @@ROWCOUNT=0 THROW 50001,'No open check-in today.',1;
 END
END;
GO
CREATE PROCEDURE dbo.ApplyLeave @StaffId int,@Start date,@End date,@Reason nvarchar(250) AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 BEGIN TRAN;
 DECLARE @role nvarchar(20),@manager int;
 SELECT @role=Role,@manager=ManagerId FROM Staff WITH(UPDLOCK,HOLDLOCK) WHERE StaffId=@StaffId;
 IF @role='HR' THROW 50001,'The demo workflow accepts employee and manager leave requests.',1;
 IF @role='Employee' AND @manager IS NULL THROW 50002,'A manager must be assigned first.',1;
 IF EXISTS(SELECT 1 FROM LeaveRequest WHERE StaffId=@StaffId AND State<>'Rejected' AND StartDate<=@End AND EndDate>=@Start)
   THROW 50003,'An existing leave request overlaps these dates.',1;
 INSERT LeaveRequest(StaffId,StartDate,EndDate,Reason,State) VALUES(@StaffId,@Start,@End,@Reason,CASE WHEN @role='Manager' AND @manager IS NULL THEN 'Pending HR' ELSE 'Pending Manager' END);
 COMMIT;
END;
GO
CREATE PROCEDURE dbo.DecideLeave @StaffId int,@Id int,@Approve bit AS
BEGIN
 SET NOCOUNT ON;
 UPDATE l SET State=CASE WHEN @Approve=0 THEN 'Rejected' WHEN reviewer.Role='Manager' THEN 'Pending HR' ELSE 'Approved' END
 FROM LeaveRequest l JOIN Staff requester ON requester.StaffId=l.StaffId
 JOIN Staff reviewer ON reviewer.StaffId=@StaffId
 WHERE l.LeaveId=@Id AND l.StaffId<>@StaffId AND
 ((reviewer.Role='Manager' AND requester.ManagerId=@StaffId AND l.State='Pending Manager') OR
 (reviewer.Role='HR' AND l.State='Pending HR'));
 IF @@ROWCOUNT=0 THROW 50004,'Request is unavailable for this approval step.',1;
END;