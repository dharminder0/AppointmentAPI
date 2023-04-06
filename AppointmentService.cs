public class AppointmentService {
 public AppointmentBookingSlot GetAvailableBookingSlots(string AppointmentCode, int PageIndex, string UserTimeZoneId, DateTime queryDate, Model.Company CompanyInfo, int UserType)
        {
            AppointmentBookingSlot appointmentBookingSlotDetails = null;
            string uiFooter = string.Empty;

            List<BookingSlotsShiftwise> masterSlots = new List<BookingSlotsShiftwise>();
            List<BookingSlotsShiftwise> vacantSlots = new List<BookingSlotsShiftwise>();
            List<BookingSlotsShiftwise> groupedSlots = new List<BookingSlotsShiftwise>();
            var NightSlots = new List<BookingSlotsShiftwise.Slots>();
            var MorningSlots = new List<BookingSlotsShiftwise.Slots>();
            var AfternoonSlots = new List<BookingSlotsShiftwise.Slots>();
            var EveningSlots = new List<BookingSlotsShiftwise.Slots>();
            var comDetailsInfo = new LeadUserResponseV2();
            List<string> ownerIds = new List<string>();


            Dictionary<DateTime, int> bookedSlotsPerDay = null;
            int sourceOwnerId = 0;
            int contactOwnerId = 0;
            int leadOwnerId = 0;


            int bookedSlotsPerWeek = 0, SlotWindowSize = Convert.ToInt32(ConfigurationManager.AppSettings["BookingSlotWindow"].ToString());

            try
            {
                using (var UOWObj = new UnitOfWork())
                {
                    var bookedAppointment = UOWObj.AppointmentBooking.GetFirstRecord(r => r.UniqueAppointmentCode == AppointmentCode && r.Status == (int)StatusEnum.Active);

                    TimeZoneInfo userTimeZoneInfo = null;

                    string calendarTimeZoneId = null;

                    if (!string.IsNullOrEmpty(UserTimeZoneId))
                        userTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(UserTimeZoneId);

                    if (bookedAppointment != null)
                    {
                        var appointmentType = bookedAppointment.AppointmentType;

                        var company = UOWObj.CompanyRepository.GetCompany(appointmentType.CompanyId);
                        var bookedAppointmentCalendar = UOWObj.CalendarRepository.GetById(company.Id, bookedAppointment.CalendarId);

                        if (company.ClientCode != null)
                        {
                            var companyObj = OWCHelper.GetCompany(company.ClientCode);
                            if (companyObj != null && !string.IsNullOrEmpty(companyObj.uiFooter))
                            {
                                uiFooter = companyObj.uiFooter;
                            }
                        }

                        if (string.IsNullOrEmpty(UserTimeZoneId))
                        {
                            userTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(bookedAppointmentCalendar.SelectedTimeZone);
                            calendarTimeZoneId = bookedAppointmentCalendar.SelectedTimeZone;
                        }

                        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(bookedAppointment.Date.Add(bookedAppointment.FromHrs), userTimeZoneInfo);
                        if (CompanyInfo == null)
                        {
                            CompanyInfo = new ServiceModel.Company()
                            {
                                JobRocketApiUrl = company.JobRocketApiUrl,
                                JobRocketApiAuthorizationBearer = company.JobRocketApiAuthorizationBearer,
                                LeadDashboardApiUrl = company.LeadDashboardApiUrl,
                                LeadDashboardApiAuthorizationBearer = company.LeadDashboardApiAuthorizationBearer,
                                PrimaryBrandingColor = company.PrimaryBrandingColor,
                                SecondaryBrandingColor = company.SecondaryBrandingColor,
                                TertiaryColor = company.TertiaryColor,
                                LogoUrl = company.LogoUrl,
                                CompanyName = company.CompanyName,
                                UiFooter = uiFooter,
                                ClientCode = company.ClientCode
                            };
                        }

                        var RescheduledNotificationTemplateId = appointmentType.EmailandSMSInAppointmentTypes.Any(r => r.NotificationType == (int)NotificationTypeEnum.RESCHEDULING) ? appointmentType.EmailandSMSInAppointmentTypes.FirstOrDefault(r => r.NotificationType == (int)NotificationTypeEnum.RESCHEDULING).Id : default(int?);
                        var CancelledNotificationTemplateId = appointmentType.EmailandSMSInAppointmentTypes.Any(r => r.NotificationType == (int)NotificationTypeEnum.CANCELLATION) ? appointmentType.EmailandSMSInAppointmentTypes.FirstOrDefault(r => r.NotificationType == (int)NotificationTypeEnum.CANCELLATION).Id : default(int?);
                        var ConfirmationNotificationTemplateId = appointmentType.EmailandSMSInAppointmentTypes.Any(r => r.NotificationType == (int)NotificationTypeEnum.CONFIRMATION) ? appointmentType.EmailandSMSInAppointmentTypes.FirstOrDefault(r => r.NotificationType == (int)NotificationTypeEnum.CONFIRMATION).Id : default(int?);

                        var schedulingLimits = UOWObj.SchedulingLimitsInCalendarRepository.GetFirstRecord(r => r.CalendarId == bookedAppointmentCalendar.Id);
                        schedulingLimits = schedulingLimits == null ? new API.Db.SchedulingLimitsInCalendar() { AllowRescheduling = true, AllowCancellation = true } : schedulingLimits;

                        var categoryDetails = OWCHelper.GetCategoryList(CompanyInfo.ClientCode);
                        //var OWCLeadUserInfo = OWCHelper.GetLeadUserInfo(bookedAppointment.LeadUserId, CompanyInfo);

                        var comDetails = OWCHelper.GetCommContactDetails(bookedAppointment.LeadUserId, CompanyInfo.ClientCode);
                        if (comDetails != null && comDetails.Any())
                        {
                            comDetailsInfo = JsonConvert.DeserializeObject<LeadUserResponseV2>(comDetails.ToString());
                        }
                        if (!comDetailsInfo.contactId.ContainsCI("SF"))
                        {
                            int.TryParse(comDetailsInfo.SourceOwnerId, out sourceOwnerId);
                            int.TryParse(comDetailsInfo.ContactOwnerId, out contactOwnerId);
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(comDetailsInfo.SourceOwnerId))
                            {
                                ownerIds.Add(comDetailsInfo.SourceOwnerId);
                            }

                            if (!string.IsNullOrWhiteSpace(comDetailsInfo.ContactOwnerId))
                            {
                                ownerIds.Add(comDetailsInfo.ContactOwnerId);
                            }

                            if (!string.IsNullOrWhiteSpace(comDetailsInfo.LeadOwnerId)) {
                                ownerIds.Add(comDetailsInfo.LeadOwnerId);
                            }

                            var externalDetails = OWCHelper.GetListOfExternalDetails(CompanyInfo.ClientCode, ownerIds);

                            if (externalDetails != null && externalDetails.Any())
                            {
                                var sourceOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.SourceOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                if (sourceOwneruserid != null)
                                {
                                    sourceOwnerId = sourceOwneruserid.Id;
                                }
                                var contactOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.ContactOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                if (contactOwneruserid != null)
                                {
                                    contactOwnerId = contactOwneruserid.Id;
                                }

                                var leadOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.LeadOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                if (leadOwneruserid != null) {
                                    leadOwnerId = leadOwneruserid.Id;
                                }

                            }

                        }

                        var leadUser = new LeadUserInfo();
                        if (comDetailsInfo != null)
                        {
                            leadUser.UserId = comDetailsInfo.contactId;
                            leadUser.FirstName = comDetailsInfo.firstName;
                            leadUser.LastName = comDetailsInfo.lastName;
                            leadUser.UserName = comDetailsInfo.email;
                            leadUser.PhoneNumber = comDetailsInfo.telephone;
                            leadUser.SourceOwnerId = sourceOwnerId;
                            leadUser.ContactOwnerId = contactOwnerId;
                            leadUser.LeadOwnerId = leadOwnerId;
                        }

                        var domainList = OWCHelper.GetClientDomains(CompanyInfo);

                        appointmentBookingSlotDetails = new AppointmentBookingSlot()
                        {
                            CalendarTimeZoneId = calendarTimeZoneId,
                            LeadUserId = bookedAppointment.LeadUserId,
                            AppointmentTypeDescription = appointmentType.AppointmentTypeDescription,
                            AppointmentTypeName = appointmentType.AppointmentTypeName,
                            Duration = appointmentType.Duration,
                            ConfirmationText = appointmentType.ConfirmationText,
                            CompanyInfo = CompanyInfo,
                            IsBooked = true,
                            OfficeInfo = OWCHelper.GetOfficeInfo(bookedAppointmentCalendar.OfficeId, CompanyInfo),
                            RescheduledNotificationTemplateId = RescheduledNotificationTemplateId,
                            CancelledNotificationTemplateId = CancelledNotificationTemplateId,
                            ConfirmationNotificationTemplateId = ConfirmationNotificationTemplateId,
                            AllowRescheduling = UserType == (int)UserTypeEnum.Recruiter ? true : schedulingLimits.AllowRescheduling,
                            AllowCancellation = UserType == (int)UserTypeEnum.Recruiter ? true : schedulingLimits.AllowCancellation,
                            Language = appointmentType.Language.HasValue ? appointmentType.Language.Value : (int)LanguageTypeEnum.Dutch,
                            TagCode = (appointmentType.CategoryId.HasValue && categoryDetails.Any(c => c.tagId == appointmentType.CategoryId)) ? categoryDetails.FirstOrDefault(c => c.tagId == appointmentType.CategoryId).tagCode : string.Empty,
                            LeadUser = leadUser,
                            GtmCode = domainList != null && domainList.Any() ? domainList.FirstOrDefault().GtmCode : null,
                            FavoriteIconUrl = domainList != null && domainList.Any() ? domainList.FirstOrDefault().FavoriteIconUrl : null,
                            BookingSlots = new List<BookingSlots>()
                            {
                                new BookingSlots
                                {
                                    Date = localDateTime.Date,
                                    TimeSlots = new List<Slots>
                                    {
                                        new Slots
                                        {
                                            IsSelected = true,
                                            Time = localDateTime.TimeOfDay
                                        }
                                    }
                                }
                            }
                        };
                    }

                    else
                    {
                        var appointmentInvitation = UOWObj.AppointmentInvitation.GetFirstRecord(r => r.Code == AppointmentCode);

                        if (appointmentInvitation == null)
                        {
                            Status = ResultEnum.Error;
                            ErrorMessage = "Appointment invitation not found for code " + AppointmentCode;
                            return null;
                        }
                        else
                        {
                            var appointmentType = appointmentInvitation.AppointmentType;

                            var company = UOWObj.CompanyRepository.GetCompany(appointmentType.CompanyId);
                            if (company.ClientCode != null)
                            {
                                var companyObj = OWCHelper.GetCompany(company.ClientCode);
                                if (companyObj != null && !string.IsNullOrEmpty(companyObj.uiFooter))
                                {
                                    uiFooter = companyObj.uiFooter;
                                }
                            }

                            if (CompanyInfo == null)
                            {
                                CompanyInfo = new ServiceModel.Company()
                                {
                                    JobRocketApiUrl = company.JobRocketApiUrl,
                                    JobRocketApiAuthorizationBearer = company.JobRocketApiAuthorizationBearer,
                                    LeadDashboardApiUrl = company.LeadDashboardApiUrl,
                                    LeadDashboardApiAuthorizationBearer = company.LeadDashboardApiAuthorizationBearer,
                                    PrimaryBrandingColor = company.PrimaryBrandingColor,
                                    SecondaryBrandingColor = company.SecondaryBrandingColor,
                                    TertiaryColor = company.TertiaryColor,
                                    LogoUrl = company.LogoUrl,
                                    CompanyName = company.CompanyName,
                                    UiFooter = uiFooter,
                                    ClientCode = company.ClientCode
                                };
                            }

                            //var OWCLeadUserInfo = OWCHelper.GetLeadUserInfo(appointmentInvitation.LeadUserId, CompanyInfo);
                            var calendarList = UOWObj.CalendarRepository.GetByAppointmentTypeId(appointmentType.CompanyId, appointmentType.Id);
                            var comDetails = OWCHelper.GetCommContactDetails(appointmentInvitation.LeadUserId, CompanyInfo.ClientCode);
                            if (comDetails != null && comDetails.Any())
                            {
                                comDetailsInfo = JsonConvert.DeserializeObject<LeadUserResponseV2>(comDetails.ToString());
                            }

                            if (appointmentInvitation.ConfigurationDetailsId > 0)
                            {

                                if (comDetailsInfo != null && comDetailsInfo.SerializeObjectWithoutNull() != "{}")
                                {
                                    if (!comDetailsInfo.contactId.ContainsCI("SF"))
                                    {
                                        int.TryParse(comDetailsInfo.SourceOwnerId, out sourceOwnerId);
                                        int.TryParse(comDetailsInfo.ContactOwnerId, out contactOwnerId);
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrWhiteSpace(comDetailsInfo.SourceOwnerId))
                                        {
                                            ownerIds.Add(comDetailsInfo.SourceOwnerId);
                                        }
                                        if (!string.IsNullOrWhiteSpace(comDetailsInfo.ContactOwnerId))
                                        {
                                            ownerIds.Add(comDetailsInfo.ContactOwnerId);
                                        }
										if (!string.IsNullOrWhiteSpace(comDetailsInfo.LeadOwnerId)) {
                                            ownerIds.Add(comDetailsInfo.LeadOwnerId);
                                        }
                                        var externalDetails = OWCHelper.GetListOfExternalDetails(CompanyInfo.ClientCode, ownerIds);
                                        if (externalDetails != null && externalDetails.Any())
                                        {
                                            var sourceOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.SourceOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                            if (sourceOwneruserid != null)
                                            {
                                                sourceOwnerId = sourceOwneruserid.Id;
                                            }
                                            var contactOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.ContactOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                            if (contactOwneruserid != null)
                                            {
                                                contactOwnerId = contactOwneruserid.Id;
                                            }

                                            var leadOwneruserid = externalDetails.Where(v => v.ExternalUserId.Equals(comDetailsInfo.LeadOwnerId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                                            if (leadOwneruserid != null) {
                                                leadOwnerId = leadOwneruserid.Id;
                                            }

                                        }
                                    }
                                }

                                var invitationConfiguredCalendarIds = UOWObj.CalendarInConfigurationDetailsRepository.GetConfiguredCalendarIds(appointmentInvitation.Id);
                                if (appointmentInvitation.ConfigurationDetails.CalendarOption == (int)CalendarOptionEnum.PreDefined && invitationConfiguredCalendarIds.Any())
                                {
                                    calendarList = calendarList.Where(c => invitationConfiguredCalendarIds.Contains(c.Id));
                                }
                                else if (appointmentInvitation.ConfigurationDetails.CalendarOption == (int)CalendarOptionEnum.CaseOwner && sourceOwnerId != 0)
                                {
                                    calendarList = calendarList.Where(a => a.CalendarOwnerId == sourceOwnerId);
                                }
                                else if (appointmentInvitation.ConfigurationDetails.CalendarOption == (int)CalendarOptionEnum.ContactOwner && contactOwnerId != 0)
                                {
                                    calendarList = calendarList.Where(a => a.CalendarOwnerId == contactOwnerId);
                                }
                                else if (appointmentInvitation.ConfigurationDetails.CalendarOption == (int)CalendarOptionEnum.LeadOwner && leadOwnerId != 0)
                                {
                                    calendarList = calendarList.Where(a => a.CalendarOwnerId == leadOwnerId);
                                }
                            }
                            else
                            {
                                var invitationSubscribingCalendarIds = appointmentInvitation.CalendarsInAppointmentInvitation.Select(c => c.calendarId);
                                if (invitationSubscribingCalendarIds.Any())
                                    calendarList = calendarList.Where(r => invitationSubscribingCalendarIds.Contains(r.Id));
                            }

                            //var calendarList = appointmentInvitation.ConfigurationDetailsId > 0 && appointmentInvitation.ConfigurationDetails.CalendarInConfigurationDetails.Any() ?
                            //    appointmentType.AppointmentTypesInCalendar.Where(cid => cid.Calendar.Status == (int)StatusEnum.Active && appointmentInvitation.ConfigurationDetails.CalendarInConfigurationDetails.Any(c => c.CalendarId == cid.CalendarId)).Select(cid => cid.Calendar).ToList()
                            //    : (appointmentInvitation.CalendarsInAppointmentInvitation.Any()
                            //   ? appointmentType.AppointmentTypesInCalendar.Where(cid => cid.Calendar.Status == (int)StatusEnum.Active && appointmentInvitation.CalendarsInAppointmentInvitation.Any(c => c.calendarId == cid.CalendarId)).Select(cid => cid.Calendar).ToList()
                            //   : appointmentType.AppointmentTypesInCalendar.Where(cid => cid.Calendar.Status == (int)StatusEnum.Active).Select(cid => cid.Calendar).ToList());

                            var schedulesInAppointmentType = appointmentType.SchedulesInAppointmentType;
                            var calendarIds = calendarList.Select(c => c.Id);
                            var calendarBookings = UOWObj.AppointmentBooking.GetByCalendarIds(calendarIds);
                            var calendarSchedulingLimits = UOWObj.SchedulingLimitsInCalendarRepository.Get(c => calendarIds.Contains(c.CalendarId));

                            var calendarSpecificDayConfigs = UOWObj.SpecificDayConfigurationRepository.Get(r => calendarIds.Contains(r.CalendarId) && (r.AppointmentTypeInSpecificDayConfiguration.Any(a => a.AppointmentTypeId == appointmentType.Id) || r.IsClosed == true));
                            var calendarSpecificDayConfigsForAllAppointmentTypes = UOWObj.SpecificDayConfigurationRepository.Get(r => calendarIds.Contains(r.CalendarId) && !(r.AppointmentTypeInSpecificDayConfiguration.Any(a => a.AppointmentTypeId == appointmentType.Id) || r.IsClosed == true));
                            var calendarOffTimeConfigs = UOWObj.OffTimeConfigurationRepository.Get(r => calendarIds.Contains(r.CalendarId));
                            var calendarIntegratedDetails = UOWObj.CalendarIntegratedDetailsRepository.Get(r => calendarIds.Contains(r.CalendarId) && r.Status == (int)StatusEnum.Active && (!string.IsNullOrEmpty(r.UserTokens.OutlookOffice365AccessToken) || !string.IsNullOrEmpty(r.UserTokens.OutlookAccessToken) || !string.IsNullOrEmpty(r.UserTokens.GoogleAccessToken)) && !string.IsNullOrEmpty(r.IntegratedCalendarId) && r.ShowDatatoLead);
                            var calendarRegularTimeConfigs = UOWObj.RegularTimeConfigurationRepository.GetByCalendarAndType(calendarIds, appointmentType.Id);

                            List<API.Db.AppointmentBooking> lstAppointmentBookings = new List<API.Db.AppointmentBooking>();

                            foreach (var calendar in calendarList.OrderBy(r => calendarBookings.Count(c => c.CalendarId == r.Id && c.AppointmentTypeId == appointmentType.Id)))
                            {
                                if (string.IsNullOrEmpty(UserTimeZoneId) && string.IsNullOrEmpty(calendarTimeZoneId))
                                {
                                    userTimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(calendar.SelectedTimeZone);
                                    calendarTimeZoneId = calendar.SelectedTimeZone;
                                }

                                var calendarTimeZone = TimeZoneInfo.FindSystemTimeZoneById(calendar.SelectedTimeZone);

                                var date = TimeZoneInfo.ConvertTime(DateTime.UtcNow, calendarTimeZone).Date;

                                var schedulingLimits = calendarSchedulingLimits.FirstOrDefault(c => c.CalendarId == calendar.Id);
                                schedulingLimits = schedulingLimits == null ? new API.Db.SchedulingLimitsInCalendar() { AllowRescheduling = true, AllowCancellation = true } : schedulingLimits;

                                vacantSlots = new List<BookingSlotsShiftwise>();

                                bookedSlotsPerWeek = 0;

                                var lstRegularTimeConfig = calendarRegularTimeConfigs.Where(r => r.CalendarId == calendar.Id);

                                var lstSpecificDayConfig = calendarSpecificDayConfigs.Where(r => r.CalendarId == calendar.Id);

                                var lstSpecificDayConfigforAllAppointmentType = calendarSpecificDayConfigsForAllAppointmentTypes.Where(r => r.CalendarId == calendar.Id);

                                lstAppointmentBookings.AddRange(calendarBookings.Where(b => b.CalendarId == calendar.Id));

                                var OffTimeConfigurationList = calendarOffTimeConfigs.Where(r => r.CalendarId == calendar.Id);

                                List<OffTimeConfig> lstOffTimeConfig = new List<OffTimeConfig>();

                                #region calculating OffTimeConfig

                                foreach (var item in OffTimeConfigurationList)
                                {
                                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(item.SelectedTimeZone);

                                    var localFromDate = TimeZoneInfo.ConvertTimeFromUtc(item.FromDate.Add(item.FromHrs), timeZone);
                                    var localToDate = TimeZoneInfo.ConvertTimeFromUtc(item.ToDate.Add(item.ToHrs), timeZone);

                                    if (item.RepeatDayId.HasValue)
                                    {
                                        int daysUntilNextDayOfWeek = (int)localFromDate.DayOfWeek > item.RepeatDayId.Value
                                                            ? (item.RepeatDayId.Value - (int)localFromDate.DayOfWeek + 7)
                                                            : item.RepeatDayId.Value - (int)localFromDate.DayOfWeek;
                                        localFromDate = localFromDate.AddDays(daysUntilNextDayOfWeek);
                                    }

                                    var fromDate = TimeZoneInfo.ConvertTimeToUtc(localFromDate.Date.Add(localFromDate.TimeOfDay), timeZone);
                                    var toDate = TimeZoneInfo.ConvertTimeToUtc(localFromDate.Date.Add(localToDate.TimeOfDay), timeZone);

                                    while (localFromDate.Date <= localToDate.Date)
                                    {
                                        if (fromDate.Date != toDate.Date)
                                        {
                                            lstOffTimeConfig.Add(new OffTimeConfig
                                            {
                                                CalendarId = item.CalendarId,
                                                Date = fromDate.Date,
                                                FromHrs = fromDate.TimeOfDay,
                                                ToHrs = new TimeSpan(23, 59, 59)
                                            });

                                            lstOffTimeConfig.Add(new OffTimeConfig
                                            {
                                                CalendarId = item.CalendarId,
                                                Date = toDate.Date,
                                                FromHrs = new TimeSpan(),
                                                ToHrs = toDate.TimeOfDay
                                            });
                                        }
                                        else
                                        {
                                            lstOffTimeConfig.Add(new OffTimeConfig
                                            {
                                                CalendarId = item.CalendarId,
                                                Date = fromDate.Date,
                                                FromHrs = fromDate.TimeOfDay,
                                                ToHrs = toDate.TimeOfDay
                                            });
                                        }

                                        if (item.RepeatDayId.HasValue)
                                            localFromDate = localFromDate.Date.AddDays(7);
                                        else
                                            localFromDate = localFromDate.Date.AddDays(1);

                                        var nextLocalStartDate = localFromDate.Date.Add(TimeZoneInfo.ConvertTimeFromUtc(item.FromDate.Add(item.FromHrs), timeZone).TimeOfDay);
                                        var nextLocalEndDate = localFromDate.Date.Add(TimeZoneInfo.ConvertTimeFromUtc(item.ToDate.Add(item.ToHrs), timeZone).TimeOfDay);
                                        var adjustmentRules = timeZone.GetAdjustmentRules();

                                        if (timeZone.IsInvalidTime(nextLocalStartDate) && timeZone.IsInvalidTime(nextLocalEndDate))
                                        {
                                            if (item.RepeatDayId.HasValue)
                                                localFromDate = localFromDate.Date.AddDays(7);
                                            else
                                                localFromDate = localFromDate.Date.AddDays(1);

                                        }
                                        else if (timeZone.IsInvalidTime(nextLocalStartDate))
                                        {
                                            nextLocalStartDate = nextLocalStartDate.Date.Add(adjustmentRules[0].DaylightTransitionEnd.TimeOfDay.TimeOfDay);
                                            if (nextLocalStartDate >= nextLocalEndDate)
                                            {
                                                if (item.RepeatDayId.HasValue)
                                                    localFromDate = localFromDate.Date.AddDays(7);
                                                else
                                                    localFromDate = localFromDate.Date.AddDays(1);
                                            }
                                        }
                                        else if (timeZone.IsInvalidTime(nextLocalEndDate))
                                        {
                                            nextLocalEndDate = nextLocalStartDate.Date.Add(adjustmentRules[0].DaylightTransitionStart.TimeOfDay.TimeOfDay);
                                            if (nextLocalStartDate >= nextLocalEndDate)
                                            {
                                                if (item.RepeatDayId.HasValue)
                                                    localFromDate = localFromDate.Date.AddDays(7);
                                                else
                                                    localFromDate = localFromDate.Date.AddDays(1);
                                            }
                                        }

                                        fromDate = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localFromDate.Date.Add(nextLocalStartDate.TimeOfDay), DateTimeKind.Unspecified), timeZone);
                                        toDate = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localFromDate.Date.Add(nextLocalEndDate.TimeOfDay), DateTimeKind.Unspecified), timeZone);
                                    }
                                }

                                #endregion

                                DateTime utcTimeNow = DateTime.UtcNow;

                                var monthStartDate = PageIndex == -1 ? TimeZoneInfo.ConvertTimeToUtc(new DateTime(queryDate.Year, queryDate.Month, 1), userTimeZoneInfo) : new DateTime(queryDate.Year, queryDate.Month, 1);
                                var monthEndDate = PageIndex == -1 ? TimeZoneInfo.ConvertTimeToUtc(new DateTime(queryDate.Year, queryDate.Month, DateTime.DaysInMonth(queryDate.Year, queryDate.Month)).AddDays(1), userTimeZoneInfo) : new DateTime(queryDate.Year, queryDate.Month, DateTime.DaysInMonth(queryDate.Year, queryDate.Month));
                                DateTime dateToStart = utcTimeNow.Add(new TimeSpan(schedulingLimits.MinTimeGapInBooking, 0, 0));

                                DateTime dateToEnd = PageIndex == -1 ? monthEndDate : utcTimeNow.AddDays(schedulingLimits.MaxTimeGapInBooking);

                                if (schedulingLimits.AllowMaxDateInBooking && schedulingLimits.MaxDateInBooking.HasValue)
                                {
                                    dateToEnd = dateToEnd <= schedulingLimits.MaxDateInBooking.Value ? dateToEnd : (schedulingLimits.MaxDateInBooking.Value);
                                }

                                DateTime currentDate = dateToStart;

                                #region integrate leaduserinfo section

                                var calendarIntegratedDetailsList = calendarIntegratedDetails.Where(r => r.CalendarId == calendar.Id);
                                List<Event> externalEventsList = new List<Event>();

                                List<FreeBusy> externalFreeBusyEventsList = new List<FreeBusy>();

                                if (calendarIntegratedDetailsList.Any())
                                {
                                    if (string.IsNullOrEmpty(company.ServiceAccountAccessToken))
                                    {
                                        foreach (var calendarIntegratedDetailsObj in calendarIntegratedDetailsList)
                                        {
                                            IEnumerable<string> cal = new List<string>() { calendarIntegratedDetailsObj.IntegratedCalendarId.ToString() };
                                            var eventList = IntegrationService.ReadEvents(cal, calendarIntegratedDetailsObj.UserTokens.OutlookOffice365AccessToken ?? calendarIntegratedDetailsObj.UserTokens.OutlookAccessToken ?? calendarIntegratedDetailsObj.UserTokens.GoogleAccessToken, calendarIntegratedDetailsObj.Type, Convert.ToDateTime(dateToStart.ToString("yyyy-MM-ddTHH:mm:ssZ")), Convert.ToDateTime(dateToEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")), UOWObj);
                                            if (eventList != null && eventList.Any())
                                                externalEventsList.AddRange(eventList.Where(r => r.Transparency == "opaque"));
                                        }
                                    }
                                    else
                                    {
                                        foreach (var calendarIntegratedDetailsObj in calendarIntegratedDetailsList)
                                        {
                                            IEnumerable<string> cal = new List<string>() { calendarIntegratedDetailsObj.IntegratedCalendarId.ToString() };
                                            var eventList = IntegrationService.ReadFreeBusyEvents(cal, calendarIntegratedDetailsObj.UserTokens.OutlookOffice365AccessToken ?? calendarIntegratedDetailsObj.UserTokens.OutlookAccessToken ?? calendarIntegratedDetailsObj.UserTokens.GoogleAccessToken, calendarIntegratedDetailsObj.Type, Convert.ToDateTime(dateToStart.ToString("yyyy-MM-ddTHH:mm:ssZ")), Convert.ToDateTime(dateToEnd.ToString("yyyy-MM-ddTHH:mm:ssZ")), UOWObj);
                                            if (eventList != null && eventList.Any())
                                                externalFreeBusyEventsList.AddRange(eventList.Where(r => r.FreeBusyStatus == "busy" || r.FreeBusyStatus == "tentative"));
                                        }
                                    }
                                }

                                #endregion


                                #region for SlotBased

                                if (appointmentType.BookingMode == 1)
                                {
                                    int totalSlots = 0;

                                    while ((PageIndex == -1 || (PageIndex > 0 && totalSlots <= (SlotWindowSize * PageIndex))) && currentDate <= dateToEnd)
                                    {
                                        bookedSlotsPerDay = new Dictionary<DateTime, int>();

                                        var weekStartDate = currentDate.Date.AddDays((int)currentDate.Date.DayOfWeek == 0 ? -6 : 1 - (int)currentDate.Date.DayOfWeek); //previous Monday
                                        var weekEndDate = currentDate.Date.AddDays((int)currentDate.Date.DayOfWeek == 0 ? 0 : 7 - (int)currentDate.Date.DayOfWeek); //coming Sunday

                                        bookedSlotsPerWeek = lstAppointmentBookings.Where(r => r.Date >= weekStartDate.Date && r.Date <= weekEndDate.Date).Count();

                                        var specificDayConfigList = lstSpecificDayConfig.Where(r => !r.IsClosed && r.Date == currentDate.Date).ToList();

                                        if (specificDayConfigList.Any(r => r.ToHrs == new TimeSpan(0)))
                                        {
                                            specificDayConfigList.AddRange(lstSpecificDayConfig.Where(r => !r.IsClosed && r.Date == currentDate.Date.AddDays(1) && r.FromHrs == new TimeSpan(0)));
                                        }

                                        if (lstSpecificDayConfigforAllAppointmentType.Any(r => r.Date == currentDate.Date) && !specificDayConfigList.Any())
                                        { }
                                        else
                                        {

                                            if (specificDayConfigList.Count > 0)
                                            {
                                                for (int i = 0; i < specificDayConfigList.Count; i++)
                                                {
                                                    if (i < specificDayConfigList.Count - 1 && specificDayConfigList[i].ToHrs == specificDayConfigList[i + 1].FromHrs && specificDayConfigList[i + 1].FromHrs == new TimeSpan(0))
                                                    {
                                                        vacantSlots.AddRange(GetSlotshiftwise(UserType, specificDayConfigList[i].FromHrs.Value, specificDayConfigList[i + 1].ToHrs.Value.Add(new TimeSpan(24, 0, 0)), specificDayConfigList[i].Date == currentDate.Date ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, specificDayConfigList[i].AppointmentTypeInSpecificDayConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                    }
                                                    else
                                                    {
                                                        vacantSlots.AddRange(GetSlotshiftwise(UserType, specificDayConfigList[i].FromHrs.Value, specificDayConfigList[i].ToHrs.Value, specificDayConfigList[i].Date == currentDate.Date ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, specificDayConfigList[i].AppointmentTypeInSpecificDayConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                    }
                                                }
                                            }
                                            else if (lstSpecificDayConfig.Where(r => r.IsClosed && r.Date == currentDate.Date).Count() == 0)
                                            {
                                                var timeConfigList = lstRegularTimeConfig.Where(r => !r.IsClosed && r.DayId == (int)currentDate.DayOfWeek).ToList();

                                                if (timeConfigList.Any(r => r.ToHrs == new TimeSpan(0)))
                                                {
                                                    timeConfigList.AddRange(lstRegularTimeConfig.Where(r => !r.IsClosed && r.DayId == ((int)currentDate.DayOfWeek == 6 ? 0 : (int)currentDate.DayOfWeek + 1) && r.FromHrs == new TimeSpan(0)).ToList());
                                                }

                                                if (timeConfigList.Count > 0)
                                                {
                                                    for (int i = 0; i < timeConfigList.Count; i++)
                                                    {
                                                        var toHrsofCurrentRow = TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i].LastUpdatedOn.Value.Date.Add(timeConfigList[i].ToHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay;
                                                        var fromHrsofCurrentRow = TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i].LastUpdatedOn.Value.Date.Add(timeConfigList[i].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay;

                                                        if (i < timeConfigList.Count - 1 && toHrsofCurrentRow == TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay && TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay == new TimeSpan(0))
                                                        {
                                                            vacantSlots.AddRange(GetSlotshiftwise(UserType, fromHrsofCurrentRow, TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].ToHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay.Add(new TimeSpan(24, 0, 0)), timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                            i = i + 1;
                                                        }
                                                        else
                                                        {
                                                            vacantSlots.AddRange(GetSlotshiftwise(UserType, fromHrsofCurrentRow, toHrsofCurrentRow, timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                        }
                                                    }
                                                }
                                            }
                                            else if (lstSpecificDayConfig.Where(r => r.IsClosed && r.Date == currentDate.Date).Count() > 0)
                                            {
                                                var timeConfigList = lstRegularTimeConfig.Where(r => !r.IsClosed && r.DayId == (int)currentDate.DayOfWeek).ToList();

                                                if (timeConfigList.Any(r => r.ToHrs == new TimeSpan(0)))
                                                {
                                                    timeConfigList.AddRange(lstRegularTimeConfig.Where(r => !r.IsClosed && r.DayId == ((int)currentDate.DayOfWeek == 6 ? 0 : (int)currentDate.DayOfWeek + 1) && r.FromHrs == new TimeSpan(0)).ToList());
                                                }

                                                if (timeConfigList.Count > 0)
                                                {
                                                    for (int i = 0; i < timeConfigList.Count; i++)
                                                    {
                                                        if (lstSpecificDayConfig.Where(r => r.Date == currentDate.Date).Count() <= 1)
                                                        {
                                                            var toHrsofCurrentRow = TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i].LastUpdatedOn.Value.Date.Add(timeConfigList[i].ToHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay;
                                                            var fromHrsofCurrentRow = TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i].LastUpdatedOn.Value.Date.Add(timeConfigList[i].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay;

                                                            TimeSpan? currentSpecificDayDataFromHrs = lstSpecificDayConfig.FirstOrDefault(r => r.Date == currentDate.Date) != null ? lstSpecificDayConfig.FirstOrDefault(r => r.Date == currentDate.Date).FromHrs : new TimeSpan(0);
                                                            TimeSpan? currentSpecificDayDataToHrs = lstSpecificDayConfig.FirstOrDefault(r => r.Date == currentDate.Date) != null ? lstSpecificDayConfig.FirstOrDefault(r => r.Date == currentDate.Date).ToHrs : new TimeSpan(0);

                                                            if (currentSpecificDayDataToHrs.Value == new TimeSpan(0))
                                                                currentSpecificDayDataToHrs = new TimeSpan(24, 0, 0);
                                                            //frmHr and ToHr both of  Available slots timing is belong before isclosed timing or both belongs after isclosed timing
                                                            if ((fromHrsofCurrentRow > currentSpecificDayDataFromHrs && toHrsofCurrentRow > currentSpecificDayDataFromHrs && fromHrsofCurrentRow >= currentSpecificDayDataToHrs && toHrsofCurrentRow > currentSpecificDayDataToHrs)
                                                                || (fromHrsofCurrentRow < currentSpecificDayDataFromHrs && toHrsofCurrentRow <= currentSpecificDayDataFromHrs && fromHrsofCurrentRow < currentSpecificDayDataToHrs && toHrsofCurrentRow < currentSpecificDayDataToHrs))
                                                            {
                                                                if (i < timeConfigList.Count - 1 && toHrsofCurrentRow == TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay && TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].FromHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay == new TimeSpan(0))
                                                                {
                                                                    vacantSlots.AddRange(GetSlotshiftwise(UserType, fromHrsofCurrentRow, TimeZoneInfo.ConvertTimeToUtc(date.Add(TimeZoneInfo.ConvertTimeFromUtc(timeConfigList[i + 1].LastUpdatedOn.Value.Date.Add(timeConfigList[i + 1].ToHrs.Value), calendarTimeZone).TimeOfDay), calendarTimeZone).TimeOfDay.Add(new TimeSpan(24, 0, 0)), timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                                    i = i + 1;
                                                                }
                                                                else
                                                                {
                                                                    vacantSlots.AddRange(GetSlotshiftwise(UserType, fromHrsofCurrentRow, toHrsofCurrentRow, timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                                }
                                                            }
                                                            //frmHr and ToHr both of Available slots timing is not belongs inbetween isclosed timing
                                                            else if (!(fromHrsofCurrentRow >= currentSpecificDayDataFromHrs && toHrsofCurrentRow <= currentSpecificDayDataToHrs))
                                                            {
                                                                //Available slots timing is start from middle of isclosed timing and end after isclosed timing
                                                                if (fromHrsofCurrentRow > currentSpecificDayDataFromHrs && currentSpecificDayDataToHrs < toHrsofCurrentRow)
                                                                {
                                                                    vacantSlots.AddRange(GetSlotshiftwise(UserType, currentSpecificDayDataToHrs.Value, toHrsofCurrentRow, timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                                }
                                                                //Available slots timing is start from before isclosed timing and end in middle of isclosed timing
                                                                else if (fromHrsofCurrentRow < currentSpecificDayDataFromHrs && currentSpecificDayDataToHrs > toHrsofCurrentRow)
                                                                {
                                                                    vacantSlots.AddRange(GetSlotshiftwise(UserType, fromHrsofCurrentRow, currentSpecificDayDataFromHrs.Value, timeConfigList[i].DayId == (int)currentDate.DayOfWeek ? currentDate : currentDate.AddDays(1), appointmentType, lstAppointmentBookings, lstOffTimeConfig, calendar, ref bookedSlotsPerDay, bookedSlotsPerWeek, userTimeZoneInfo, timeConfigList[i].AppointmentTypeInRegularTimeConfiguration.FirstOrDefault(a => a.AppointmentTypeId == appointmentType.Id).MaxAppointmentPerSlot, externalEventsList, externalFreeBusyEventsList));
                                                                }
                                                                i = i + 1;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        currentDate = currentDate.Date.AddDays(1) == dateToEnd.Date ? dateToEnd.Date : currentDate.Date.AddDays(1);

                                        totalSlots = vacantSlots.Select(r => r.Date.Date).Distinct().Count();

                                        date = date.AddDays(1);
                                    }
                                }

                                #endregion

                                #region for FixedSchedule

                                //else
                                //{
                                //    if (schedulesInAppointmentType.Count > 0)
                                //    {
                                //        foreach (var schedule in schedulesInAppointmentType.Where(cid => cid.Date.Add(cid.FromHrs) >= dateToStart && cid.Date.Add(cid.FromHrs) <= dateToEnd))
                                //        {
                                //            bookedSlotsPerDay = new Dictionary<DateTime, int>();

                                //            currentDate = schedule.Date;

                                //            var bookedAppointmentObj = lstAppointmentBookings.Where(cid => cid.Date == currentDate.Date);

                                //            if (bookedAppointmentObj.Count() > 0)
                                //            {
                                //                if (bookedSlotsPerDay.Where(cid => cid.Key == currentDate.Date).Count() == 0) bookedSlotsPerDay.Add(currentDate.Date, bookedAppointmentObj.Count());
                                //                else bookedSlotsPerDay[currentDate.Date] = bookedAppointmentObj.Count();
                                //            }

                                //            if ((bookedAppointmentObj.Count() == 0 || ((!calendar.MaxAppointmentPerDay.HasValue || (calendar.MaxAppointmentPerDay.HasValue && calendar.MaxAppointmentPerDay.Value > (bookedSlotsPerDay.Where(cid => cid.Key == currentDate.Date).Count() == 0 ? -1 : bookedSlotsPerDay.FirstOrDefault(cid => cid.Key == currentDate.Date).Value))) &&
                                //              (!calendar.MaxAppointmentPerWeek.HasValue || (calendar.MaxAppointmentPerWeek.HasValue && calendar.MaxAppointmentPerWeek.Value > bookedSlotsPerWeek)))) &&
                                //              lstOffTimeConfig.Where(cid => cid.Date == currentDate.Date && schedule.FromHrs >= cid.FromHrs && schedule.FromHrs < cid.ToHrs).Count() == 0)
                                //            {
                                //                var vacant = new BookingSlotsShiftwise();
                                //                vacant.NightSlots = new List<BookingSlotsShiftwise.Slots>();
                                //                vacant.MorningSlots = new List<BookingSlotsShiftwise.Slots>();
                                //                vacant.AfternoonSlots = new List<BookingSlotsShiftwise.Slots>();
                                //                vacant.EveningSlots = new List<BookingSlotsShiftwise.Slots>();
                                //                var localDateTime = Utility.ConvertUTCDateToLocalDate(currentDate.Date.Add(schedule.FromHrs), OffsetValue);
                                //                var localDateTimeTo = localDateTime.Add(new TimeSpan(0, appointmentType.Duration + appointmentType.BlockBookingAfter + appointmentType.BlockBookingBefore, 0));
                                //                vacant.CalendarId = calendar.Id;
                                //                vacant.Date = localDateTime.Date;

                                //                if (localDateTime.TimeOfDay.Hours >= 0 && localDateTime.TimeOfDay.Hours < 6)
                                //                {
                                //                    NightSlots.Add(new BookingSlotsShiftwise.Slots
                                //                    {
                                //                        StartTime = localDateTime.TimeOfDay,
                                //                        EndTime = localDateTimeTo.TimeOfDay,
                                //                        IsSelected = false
                                //                    });

                                //                    vacant.NightSlots = NightSlots.ToList();
                                //                }
                                //                if (localDateTime.TimeOfDay.Hours >= 6 && localDateTime.TimeOfDay.Hours < 12)
                                //                {
                                //                    MorningSlots.Add(new BookingSlotsShiftwise.Slots
                                //                    {
                                //                        StartTime = localDateTime.TimeOfDay,
                                //                        EndTime = localDateTimeTo.TimeOfDay,
                                //                        IsSelected = false
                                //                    });

                                //                    vacant.MorningSlots = MorningSlots.ToList();
                                //                }
                                //                else if (localDateTime.TimeOfDay.Hours >= 12 && localDateTime.TimeOfDay.Hours < 18)
                                //                {

                                //                    AfternoonSlots.Add(new BookingSlotsShiftwise.Slots
                                //                    {
                                //                        StartTime = localDateTime.TimeOfDay,
                                //                        EndTime = localDateTimeTo.TimeOfDay,
                                //                        IsSelected = false
                                //                    });

                                //                    vacant.AfternoonSlots = AfternoonSlots.ToList();

                                //                }
                                //                else
                                //                {
                                //                    EveningSlots.Add(new BookingSlotsShiftwise.Slots
                                //                    {
                                //                        StartTime = localDateTime.TimeOfDay,
                                //                        EndTime = localDateTimeTo.TimeOfDay,
                                //                        IsSelected = false
                                //                    });

                                //                    vacant.EveningSlots = EveningSlots.ToList();

                                //                }
                                //                vacantSlots.Add(vacant);                                                
                                //            }
                                //        }

                                //        #region integrate leaduserinfo section

                                //        if (externalEventsList != null && externalEventsList.Any())
                                //        {
                                //            foreach (var evt in externalEventsList)
                                //            {
                                //                var startDate = DateTime.UtcNow;
                                //                var endDate = DateTime.UtcNow;
                                //                if (evt.Transparency == "opaque")
                                //                {
                                //                    startDate = Utility.ConvertUTCDateToLocalDate(evt.Start.DateTimeOffset.UtcDateTime, OffsetValue);
                                //                    endDate = Utility.ConvertUTCDateToLocalDate(evt.End.DateTimeOffset.UtcDateTime, OffsetValue);
                                //                }
                                //                else
                                //                {
                                //                    startDate = Utility.ConvertUTCDateToLocalDate(evt.Start.Date.DateTime, OffsetValue);
                                //                    endDate = Utility.ConvertUTCDateToLocalDate(evt.End.Date.DateTime, OffsetValue);
                                //                }

                                //                vacantSlots = (from s in vacantSlots
                                //                               select new BookingSlotsShiftwise
                                //                               {
                                //                                   CalendarId = s.CalendarId,
                                //                                   Date = s.Date,
                                //                                   IsSelectedCalendar = s.IsSelectedCalendar,
                                //                                   NightSlots = s.NightSlots.Where(cid => !(((startDate <= s.Date.Add(cid.StartTime) && s.Date.Add(cid.StartTime) < endDate)
                                //                                                                          || (s.Date.Add(cid.StartTime) <= startDate && startDate < s.Date.Add(cid.StartTime).AddMinutes(appointmentType.Duration))
                                //                                                                          || (startDate < s.Date.Add(cid.StartTime) && endDate > s.Date.Add(cid.StartTime))
                                //                                                                          || (startDate > s.Date.Add(cid.StartTime) && endDate < s.Date.Add(cid.StartTime)))
                                //                                                                          && s.CalendarId == calendar.Id)).Distinct().ToList(),
                                //                                   MorningSlots = s.MorningSlots.Where(cid => !(((startDate <= s.Date.Add(cid.StartTime) && s.Date.Add(cid.StartTime) < endDate)
                                //                                                                          || (s.Date.Add(cid.StartTime) <= startDate && startDate < s.Date.Add(cid.StartTime).AddMinutes(appointmentType.Duration))
                                //                                                                          || (startDate < s.Date.Add(cid.StartTime) && endDate > s.Date.Add(cid.StartTime))
                                //                                                                          || (startDate > s.Date.Add(cid.StartTime) && endDate < s.Date.Add(cid.StartTime)))
                                //                                                                          && s.CalendarId == calendar.Id)).Distinct().ToList(),
                                //                                   AfternoonSlots = s.AfternoonSlots.Where(cid => !(((startDate <= s.Date.Add(cid.StartTime) && s.Date.Add(cid.StartTime) < endDate)
                                //                                                                          || (s.Date.Add(cid.StartTime) <= startDate && startDate < s.Date.Add(cid.StartTime).AddMinutes(appointmentType.Duration))
                                //                                                                          || (startDate < s.Date.Add(cid.StartTime) && endDate > s.Date.Add(cid.StartTime))
                                //                                                                          || (startDate > s.Date.Add(cid.StartTime) && endDate < s.Date.Add(cid.StartTime)))
                                //                                                                          && s.CalendarId == calendar.Id)).Distinct().ToList(),
                                //                                   EveningSlots = s.EveningSlots.Where(cid => !(((startDate <= s.Date.Add(cid.StartTime) && s.Date.Add(cid.StartTime) < endDate)
                                //                                                                          || (s.Date.Add(cid.StartTime) <= startDate && startDate < s.Date.Add(cid.StartTime).AddMinutes(appointmentType.Duration))
                                //                                                                          || (startDate < s.Date.Add(cid.StartTime) && endDate > s.Date.Add(cid.StartTime))
                                //                                                                          || (startDate > s.Date.Add(cid.StartTime) && endDate < s.Date.Add(cid.StartTime)))
                                //                                                                          && s.CalendarId == calendar.Id)).Distinct().ToList()
                                //                               }).ToList();
                                //            }
                                //        }

                                //        #endregion
                                //    }
                                //}

                                #endregion

                                masterSlots.AddRange(vacantSlots);
                            }

                            var groupedResultSet = (from rows in masterSlots
                                                    group rows by rows.Date into grp
                                                    select new BookingSlotsShiftwise()
                                                    {
                                                        Date = grp.Key,
                                                        NightSlots = (from ts in grp.Where(r => r.NightSlots != null).SelectMany(r => r.NightSlots)
                                                                      group ts by ts.StartTime into g
                                                                      select g.First()).OrderBy(r => r.StartTime.Ticks).Distinct().ToList(),
                                                        MorningSlots = (from ts in grp.Where(r => r.MorningSlots != null).SelectMany(r => r.MorningSlots)
                                                                        group ts by ts.StartTime into g
                                                                        select g.First()).OrderBy(r => r.StartTime.Ticks).Distinct().ToList(),
                                                        EveningSlots = (from ts in grp.Where(r => r.EveningSlots != null).SelectMany(r => r.EveningSlots)
                                                                        group ts by ts.StartTime into g
                                                                        select g.First()).OrderBy(r => r.StartTime.Ticks).Distinct().ToList(),
                                                        AfternoonSlots = (from ts in grp.Where(r => r.AfternoonSlots != null).SelectMany(r => r.AfternoonSlots)
                                                                          group ts by ts.StartTime into g
                                                                          select g.First()).OrderBy(r => r.StartTime.Ticks).Distinct().ToList()
                                                    }).OrderBy(r => r.Date).Skip(PageIndex == -1 ? 0 : (PageIndex - 1) * SlotWindowSize);


                            groupedSlots = groupedResultSet.Count() > 0 ? groupedResultSet.Take(PageIndex == -1 ? groupedResultSet.Count() : SlotWindowSize).ToList() : new List<BookingSlotsShiftwise>();

                            var categoryDetails = OWCHelper.GetCategoryList(CompanyInfo.ClientCode);

                            var ConfirmationNotificationTemplateId = appointmentType.EmailandSMSInAppointmentTypes.Any(r => r.NotificationType == (int)NotificationTypeEnum.CONFIRMATION) ? appointmentType.EmailandSMSInAppointmentTypes.FirstOrDefault(r => r.NotificationType == (int)NotificationTypeEnum.CONFIRMATION).Id : default(int?);

                            var leadUser = new LeadUserInfo();
                            if (comDetailsInfo != null)
                            {
                                leadUser.UserId = comDetailsInfo.contactId;
                                leadUser.FirstName = comDetailsInfo.firstName;
                                leadUser.LastName = comDetailsInfo.lastName;
                                leadUser.UserName = comDetailsInfo.email;
                                leadUser.PhoneNumber = comDetailsInfo.telephone;
                                leadUser.SourceOwnerId = sourceOwnerId;
                                leadUser.ContactOwnerId = contactOwnerId;
                                leadUser.LeadOwnerId = leadOwnerId;
                                //   //Todo : Dharminder
                            }

                            var domainList = OWCHelper.GetClientDomains(CompanyInfo);

                            appointmentBookingSlotDetails = new AppointmentBookingSlot()
                            {
                                CalendarTimeZoneId = calendarTimeZoneId,
                                LeadUserId = appointmentInvitation.LeadUserId,
                                AppointmentTypeDescription = appointmentType.AppointmentTypeDescription,
                                AppointmentTypeName = appointmentType.AppointmentTypeName,
                                CompanyInfo = CompanyInfo,
                                Duration = appointmentType.Duration,
                                ConfirmationText = appointmentType.ConfirmationText,
                                BookingSlotsShiftwise = groupedSlots,
                                IsBooked = false,
                                RescheduledNotificationTemplateId = null,
                                CancelledNotificationTemplateId = null,
                                ConfirmationNotificationTemplateId = ConfirmationNotificationTemplateId,
                                AllowRescheduling = true,
                                AllowCancellation = true,
                                Language = appointmentType.Language.HasValue ? appointmentType.Language.Value : (int)LanguageTypeEnum.Dutch,
                                TagCode = (appointmentType.CategoryId.HasValue && categoryDetails.Any(c => c.tagId == appointmentType.CategoryId)) ? categoryDetails.FirstOrDefault(c => c.tagId == appointmentType.CategoryId).tagCode : string.Empty,
                                LeadUser = leadUser,
                                GtmCode = domainList != null && domainList.Any() ? domainList.FirstOrDefault().GtmCode : null,
                                FavoriteIconUrl = domainList != null && domainList.Any() ? domainList.FirstOrDefault().FavoriteIconUrl : null
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Status = ResultEnum.Error;
                ErrorMessage = ex.Message;
                throw ex;
            }

            return appointmentBookingSlotDetails;
        }
}
