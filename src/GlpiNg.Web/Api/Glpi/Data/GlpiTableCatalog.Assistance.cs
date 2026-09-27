using System.Linq.Expressions;
using System.Text.Json.Nodes;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Assistance.Models;
using GlpiNg.Modules.Assistance.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    /// <summary>Assistance : tickets, problèmes, changements, leurs suivis et tâches, niveaux de service.</summary>
    private void RegisterAssistance()
    {
        Table<Ticket>("glpi_tickets", t => t.Id)
            .Entity(t => t.EntityId)
            .Col("name", t => t.Name)
            .Col("content", t => t.Content)
            .Col("date", t => t.OpenedAt)
            .Col("date_creation", t => t.OpenedAt, readOnly: true)
            .Col("date_mod", t => t.UpdatedAt, readOnly: true)
            .Col("closedate", t => t.ClosedAt)
            .Col("solvedate", t => t.SolvedAt)
            .Col("takeintoaccountdate", t => t.TakenIntoAccountAt, readOnly: true)
            .Col("status", t => t.Status)
            .Col("type", t => t.Type)
            .Col("urgency", t => t.Urgency)
            .Col("impact", t => t.Impact)
            .Col("priority", t => t.Priority)
            .Fk("itilcategories_id", t => t.CategoryId)
            .Fk("users_id_recipient", t => t.RequesterUserId)
            .Fk("slas_id_tto", t => t.SlaTimeToOwnId)
            .Fk("slas_id_ttr", t => t.SlaTimeToResolveId)
            .Fk("olas_id_tto", t => t.OlaTimeToOwnId)
            .Fk("olas_id_ttr", t => t.OlaTimeToResolveId)
            .Col("time_to_own", t => t.TimeToOwn, readOnly: true)
            // Échéance de résolution : celle du SLA (UTC), sinon celle saisie à la main (heure
            // locale, stockée telle quelle) — GLPI n'a qu'une colonne pour les deux.
            .Computed("time_to_resolve",
                (t, ctx) => t.TimeToResolve is DateTime ttr ? ctx.ToUserTime(ttr) : t.DueDate,
                (t, v, ctx) => t.DueDate = GlpiValue.TryToDateTime(v, out DateTime due) ? due : null)
            .Col("internal_time_to_own", t => t.InternalTimeToOwn, readOnly: true)
            .Col("internal_time_to_resolve", t => t.InternalTimeToResolve, readOnly: true)
            .Col("ola_tto_begin_date", t => t.OlaStartedAt, readOnly: true)
            .Col("ola_ttr_begin_date", t => t.OlaStartedAt, readOnly: true)
            .Const("is_deleted", 0L)
            .OnCreate((t, ctx) =>
            {
                t.OpenedAt = Now;
                t.RequesterUserId = ctx.UserId;
            })
            .BeforeSave(async (t, ctx, input, ct) =>
            {
                ApplyItilActors(input,
                    requester: id => t.RequesterUserId = id,
                    assignUser: id => t.AssignedUserId = id,
                    assignGroup: id => t.AssignedGroupId = id);
                ApplyItilCommon(t.Status == TicketStatus.Solved, t.Status == TicketStatus.Closed,
                    input, () => t.Priority = ItilPriorityMatrix.Compute(t.Urgency, t.Impact),
                    manual => t.IsPriorityManual = manual, t.IsPriorityManual,
                    solved => t.SolvedAt ??= solved, closed => t.ClosedAt ??= closed);
                t.UpdatedAt = Now;
                await ctx.Services.GetRequiredService<ServiceLevelService>().ApplyAsync(t, ct);
            });

        Table<Problem>("glpi_problems", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Col("content", p => p.Content)
            .Col("date", p => p.OpenedAt)
            .Col("date_creation", p => p.OpenedAt, readOnly: true)
            .Col("date_mod", p => p.UpdatedAt, readOnly: true)
            .Col("solvedate", p => p.SolvedAt)
            .Col("closedate", p => p.ClosedAt)
            .Col("time_to_resolve", p => p.DueDate, wallClock: true)
            .Col("status", p => p.Status)
            .Col("urgency", p => p.Urgency)
            .Col("impact", p => p.Impact)
            .Col("priority", p => p.Priority)
            .Fk("itilcategories_id", p => p.CategoryId)
            .Fk("users_id_recipient", p => p.AuthorUserId)
            .Col("symptomcontent", p => p.SymptomContent)
            .Col("causecontent", p => p.CauseContent)
            .Col("impactcontent", p => p.ImpactContent)
            .Const("is_deleted", 0L)
            .OnCreate((p, ctx) =>
            {
                p.OpenedAt = Now;
                p.AuthorUserId = ctx.UserId;
            })
            .BeforeSave((p, ctx, input, ct) =>
            {
                ApplyItilActors(input,
                    requester: id => p.AuthorUserId = id,
                    assignUser: id => p.AssignedUserId = id,
                    assignGroup: id => p.AssignedGroupId = id);
                ApplyItilCommon(p.Status == ProblemStatus.Solved, p.Status == ProblemStatus.Closed,
                    input, () => p.Priority = ItilPriorityMatrix.Compute(p.Urgency, p.Impact),
                    manual => p.IsPriorityManual = manual, p.IsPriorityManual,
                    solved => p.SolvedAt ??= solved, closed => p.ClosedAt ??= closed);
                p.UpdatedAt = Now;
                return Task.CompletedTask;
            });

        Table<Change>("glpi_changes", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("content", c => c.Content)
            .Col("date", c => c.OpenedAt)
            .Col("date_creation", c => c.OpenedAt, readOnly: true)
            .Col("date_mod", c => c.UpdatedAt, readOnly: true)
            .Col("solvedate", c => c.SolvedAt)
            .Col("closedate", c => c.ClosedAt)
            .Col("time_to_resolve", c => c.DueDate, wallClock: true)
            .Col("status", c => c.Status)
            .Col("urgency", c => c.Urgency)
            .Col("impact", c => c.Impact)
            .Col("priority", c => c.Priority)
            .Fk("itilcategories_id", c => c.CategoryId)
            .Fk("users_id_recipient", c => c.AuthorUserId)
            .Col("impactcontent", c => c.ImpactContent)
            .Col("controlistcontent", c => c.ControlListContent)
            .Col("rolloutplancontent", c => c.RolloutPlanContent)
            .Col("backoutplancontent", c => c.BackoutPlanContent)
            .Col("checklistcontent", c => c.ChecklistContent)
            .Col("global_validation", c => c.GlobalValidation)
            .Const("is_deleted", 0L)
            .OnCreate((c, ctx) =>
            {
                c.OpenedAt = Now;
                c.AuthorUserId = ctx.UserId;
            })
            .BeforeSave((c, ctx, input, ct) =>
            {
                ApplyItilActors(input,
                    requester: id => c.AuthorUserId = id,
                    assignUser: id => c.AssignedUserId = id,
                    assignGroup: id => c.AssignedGroupId = id);
                ApplyItilCommon(c.Status == ChangeStatus.Solved, c.Status == ChangeStatus.Closed,
                    input, () => c.Priority = ItilPriorityMatrix.Compute(c.Urgency, c.Impact),
                    manual => c.IsPriorityManual = manual, c.IsPriorityManual,
                    solved => c.SolvedAt ??= solved, closed => c.ClosedAt ??= closed);
                c.UpdatedAt = Now;
                return Task.CompletedTask;
            });

        Table<ProblemTicket>("glpi_problems_tickets", l => l.Id)
            .Where(ctx => l => ctx.Db.Set<Problem>().Any(p => p.Id == l.ProblemId))
            .Col("problems_id", l => l.ProblemId)
            .Col("tickets_id", l => l.TicketId)
            .Const("link", 1L);

        Table<ChangeTicket>("glpi_changes_tickets", l => l.Id)
            .Where(ctx => l => ctx.Db.Set<Change>().Any(c => c.Id == l.ChangeId))
            .Col("changes_id", l => l.ChangeId)
            .Col("tickets_id", l => l.TicketId)
            .Const("link", 1L);

        Table<ChangeProblem>("glpi_changes_problems", l => l.Id)
            .Where(ctx => l => ctx.Db.Set<Change>().Any(c => c.Id == l.ChangeId))
            .Col("changes_id", l => l.ChangeId)
            .Col("problems_id", l => l.ProblemId)
            .Const("link", 1L);

        Table<ItilFollowup>("glpi_itilfollowups", f => f.Id)
            .Where(ctx => f =>
                (f.ItemType == ItemTypes.Ticket && ctx.Db.Set<Ticket>().Any(t => t.Id == f.ItemId))
                || (f.ItemType == ItemTypes.Problem && ctx.Db.Set<Problem>().Any(p => p.Id == f.ItemId))
                || (f.ItemType == ItemTypes.Change && ctx.Db.Set<Change>().Any(c => c.Id == f.ItemId)))
            .Col("itemtype", f => f.ItemType)
            .Col("items_id", f => f.ItemId)
            .Col("content", f => f.Content)
            .Col("is_private", f => f.IsPrivate)
            .Col("date", f => f.CreatedAt, readOnly: true)
            .Col("date_creation", f => f.CreatedAt, readOnly: true)
            .Col("date_mod", f => f.CreatedAt, readOnly: true)
            .Named("users_id", f => f.AuthorName, Users, DisplayNameColumn, readOnly: true)
            .OnCreate((f, ctx) =>
            {
                f.CreatedAt = Now;
                f.AuthorName = ctx.UserDisplayName;
            });

        ItilTaskTable("glpi_tickettasks", ItemTypes.Ticket, "tickets_id");
        ItilTaskTable("glpi_problemtasks", ItemTypes.Problem, "problems_id");
        ItilTaskTable("glpi_changetasks", ItemTypes.Change, "changes_id");

        Table<ChangeValidation>("glpi_changevalidations", v => v.Id)
            .Where(ctx => v => ctx.Db.Set<Change>().Any(c => c.Id == v.ChangeId))
            .Entity(v => v.Change!.EntityId, readOnly: true)
            .Col("changes_id", v => v.ChangeId)
            .Col("users_id_validate", v => v.ValidatorUserId)
            .Col("status", v => v.Status)
            .Col("comment_submission", v => v.RequestComment)
            .Col("comment_validation", v => v.ValidationComment)
            .Col("submission_date", v => v.SubmittedAt, readOnly: true)
            .Col("validation_date", v => v.ValidatedAt)
            .OnCreate((v, ctx) =>
            {
                v.SubmittedAt = Now;
                v.RequesterName = ctx.UserDisplayName;
            });

        // Solutions : GlpiNg les porte sur l'objet ITIL, GLPI dans sa propre table.
        Union("glpi_itilsolutions",
            ItilSolutionPart<Ticket>(ItemTypes.Ticket, t => t.Id, t => t.Solution, t => t.SolutionType, t => t.SolvedAt, t => t.Status == TicketStatus.Closed),
            ItilSolutionPart<Problem>(ItemTypes.Problem, p => p.Id, p => p.Solution, p => p.SolutionType, p => p.SolvedAt, p => p.Status == ProblemStatus.Closed),
            ItilSolutionPart<Change>(ItemTypes.Change, c => c.Id, c => c.Solution, c => c.SolutionType, c => c.SolvedAt, c => c.Status == ChangeStatus.Closed));

        // Acteurs : GlpiNg porte un demandeur et un technicien par objet, GLPI une ligne par acteur.
        Union("glpi_tickets_users",
            ActorPart<Ticket>("glpi_tickets_users", "tickets_id", t => t.Id, t => t.RequesterUserId, 1),
            ActorPart<Ticket>("glpi_tickets_users", "tickets_id", t => t.Id, t => t.AssignedUserId, 2));
        Union("glpi_problems_users",
            ActorPart<Problem>("glpi_problems_users", "problems_id", p => p.Id, p => p.AuthorUserId, 1),
            ActorPart<Problem>("glpi_problems_users", "problems_id", p => p.Id, p => p.AssignedUserId, 2));
        Union("glpi_changes_users",
            ActorPart<Change>("glpi_changes_users", "changes_id", c => c.Id, c => c.AuthorUserId, 1),
            ActorPart<Change>("glpi_changes_users", "changes_id", c => c.Id, c => c.AssignedUserId, 2));
        Union("glpi_groups_tickets", GroupActorPart<Ticket>("glpi_groups_tickets", "tickets_id", t => t.Id, t => t.AssignedGroupId));
        Union("glpi_groups_problems", GroupActorPart<Problem>("glpi_groups_problems", "problems_id", p => p.Id, p => p.AssignedGroupId));
        Union("glpi_changes_groups", GroupActorPart<Change>("glpi_changes_groups", "changes_id", c => c.Id, c => c.AssignedGroupId));

        Table<TicketCategory>("glpi_itilcategories", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Fk("itilcategories_id", c => c.ParentId)
            .Tree("itilcategories_id")
            .Col("comment", c => c.Comment)
            .Col("is_helpdeskvisible", c => c.IsActive)
            .Const("is_incident", 1L)
            .Const("is_request", 1L)
            .Const("is_problem", 1L)
            .Const("is_change", 1L)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v)
            .OnCreate((c, ctx) => c.IsActive = true);

        Table<ServiceLevel>("glpi_slms", s => s.Id)
            .Entity(s => s.EntityId, s => s.IsRecursive)
            .Col("name", s => s.Name)
            .Col("comment", s => s.Comment)
            .Fk("calendars_id", s => s.CalendarId)
            .Col("use_ticket_calendar", s => s.CalendarId == null, readOnly: true)
            .Dates(s => s.CreatedAt, s => s.UpdatedAt, (s, v) => s.CreatedAt = v, (s, v) => s.UpdatedAt = v);

        AgreementTable("glpi_slas", ServiceLevelKind.Sla);
        AgreementTable("glpi_olas", (ServiceLevelKind)2);
        EscalationTable("glpi_slalevels", "slas_id", ServiceLevelKind.Sla);
        EscalationTable("glpi_olalevels", "olas_id", (ServiceLevelKind)2);

        Table<Calendar>("glpi_calendars", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v);

        Table<CalendarSegment>("glpi_calendarsegments", s => s.Id)
            .Where(ctx => s => ctx.Db.Set<Calendar>().Any(c => c.Id == s.CalendarId))
            .Entity(s => s.Calendar!.EntityId, readOnly: true)
            .Col("calendars_id", s => s.CalendarId)
            // GLPI numérote les jours comme DayOfWeek : 0 = dimanche.
            .Col("day", s => s.DayOfWeek)
            .Col("begin", s => s.StartTime)
            .Col("end", s => s.EndTime);

        Table<CalendarHoliday>("glpi_holidays", h => h.Id)
            .Where(ctx => h => ctx.Db.Set<Calendar>().Any(c => c.Id == h.CalendarId))
            .Entity(h => h.Calendar!.EntityId, readOnly: true)
            .Col("name", h => h.Name)
            .Col("begin_date", h => h.StartDate)
            .Col("end_date", h => h.EndDate)
            .Col("is_perpetual", h => h.IsPerpetual)
            .ReadOnly();

        Table<CalendarHoliday>("glpi_calendars_holidays", h => h.Id)
            .Where(ctx => h => ctx.Db.Set<Calendar>().Any(c => c.Id == h.CalendarId))
            .Col("calendars_id", h => h.CalendarId, readOnly: true)
            .Col("holidays_id", h => h.Id, readOnly: true)
            .ReadOnly();
    }

    private void ItilTaskTable(string table, string itemtype, string parentColumn)
    {
        Table<ItilTask>(table, t => t.Id)
            .Where(t => t.ItemType == itemtype)
            .Where(ctx => itemtype == ItemTypes.Ticket
                ? t => ctx.Db.Set<Ticket>().Any(x => x.Id == t.ItemId)
                : itemtype == ItemTypes.Problem
                    ? t => ctx.Db.Set<Problem>().Any(x => x.Id == t.ItemId)
                    : t => ctx.Db.Set<Change>().Any(x => x.Id == t.ItemId))
            .Col(parentColumn, t => t.ItemId)
            .Col("content", t => t.Content)
            // GLPI : 0 = information, 1 = à faire, 2 = fait — ItilTaskState reprend 1 et 2.
            .Col("state", t => t.State)
            .Fk("users_id_tech", t => t.AssignedUserId)
            .Col("begin", t => t.PlannedStart, wallClock: true)
            .Col("end", t => t.PlannedEnd, wallClock: true)
            .Computed("actiontime", (t, ctx) => (long)(t.DurationMinutes ?? 0) * 60,
                (t, v, ctx) => t.DurationMinutes = GlpiValue.TryToLong(v, out long seconds) ? (int)(seconds / 60) : null)
            .Col("date", t => t.CreatedAt, readOnly: true)
            .Col("date_creation", t => t.CreatedAt, readOnly: true)
            .Col("date_mod", t => t.CompletedAt ?? t.CreatedAt, readOnly: true)
            .Named("users_id", t => t.AuthorName, Users, DisplayNameColumn, readOnly: true)
            .Const("is_private", 0L)
            .OnCreate((t, ctx) =>
            {
                t.ItemType = itemtype;
                t.CreatedAt = Now;
                t.AuthorName = ctx.UserDisplayName;
            })
            .OnSave((t, ctx) => t.CompletedAt = t.State == ItilTaskState.Done ? t.CompletedAt ?? Now : null);
    }

    private static IGlpiTable ItilSolutionPart<T>(string itemtype, Expression<Func<T, int>> id,
        Expression<Func<T, string?>> solution, Expression<Func<T, string?>> type,
        Expression<Func<T, DateTime?>> solvedAt, Expression<Func<T, bool>> accepted) where T : class
    {
        ParameterExpression p = solution.Parameters[0];
        Expression<Func<T, bool>> hasSolution = Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(
                Expression.NotEqual(solution.Body, Expression.Constant(null, typeof(string))),
                Expression.NotEqual(solution.Body, Expression.Constant(string.Empty))), p);

        Func<T, bool> isAccepted = accepted.Compile();
        return Part("glpi_itilsolutions", id)
            .Where(hasSolution)
            .Const("itemtype", itemtype)
            .Col("items_id", id, readOnly: true)
            .Col("content", solution, readOnly: true)
            .Col("solutiontype_name", type, readOnly: true)
            .Col("date_creation", solvedAt, readOnly: true)
            .Col("date_mod", solvedAt, readOnly: true)
            // GLPI : 2 = en attente d'approbation, 3 = acceptée.
            .Computed("status", (e, ctx) => isAccepted(e) ? 3L : 2L)
            .ReadOnly()
            .Table;
    }

    private static IGlpiTable ActorPart<T>(string table, string parentColumn, Expression<Func<T, int>> id,
        Expression<Func<T, int?>> user, int type) where T : class
    {
        ParameterExpression p = user.Parameters[0];
        Expression<Func<T, bool>> hasUser = Expression.Lambda<Func<T, bool>>(
            Expression.NotEqual(user.Body, Expression.Constant(null, typeof(int?))), p);
        return Part(table, id)
            .Where(hasUser)
            .Col(parentColumn, id, readOnly: true)
            .Fk("users_id", user, readOnly: true)
            .Const("type", (long)type)
            .Const("use_notification", 1L)
            .ReadOnly()
            .Table;
    }

    private static IGlpiTable GroupActorPart<T>(string table, string parentColumn, Expression<Func<T, int>> id,
        Expression<Func<T, int?>> group) where T : class
    {
        ParameterExpression p = group.Parameters[0];
        Expression<Func<T, bool>> hasGroup = Expression.Lambda<Func<T, bool>>(
            Expression.NotEqual(group.Body, Expression.Constant(null, typeof(int?))), p);
        return Part(table, id)
            .Where(hasGroup)
            .Col(parentColumn, id, readOnly: true)
            .Fk("groups_id", group, readOnly: true)
            .Const("type", 2L)
            .ReadOnly()
            .Table;
    }

    private void AgreementTable(string table, ServiceLevelKind kind)
    {
        Table<ServiceLevelAgreement>(table, a => a.Id)
            .Where(a => a.Kind == kind)
            .Where(ctx => a => ctx.Db.Set<ServiceLevel>().Any(s => s.Id == a.ServiceLevelId))
            .Entity(a => a.ServiceLevel!.EntityId, readOnly: true)
            .Col("name", a => a.Name)
            .Col("slms_id", a => a.ServiceLevelId)
            // GLPI : 0 = temps de résolution, 1 = temps de prise en charge — comme ServiceLevelTarget.
            .Col("type", a => a.Target)
            .Col("number_time", a => a.DurationValue)
            .Computed("definition_time",
                (a, ctx) => a.DurationUnit switch { ServiceLevelUnit.Minute => "minute", ServiceLevelUnit.Hour => "hour", _ => "day" },
                (a, v, ctx) => a.DurationUnit = GlpiValue.ToText(v) switch { "minute" => ServiceLevelUnit.Minute, "hour" => ServiceLevelUnit.Hour, _ => ServiceLevelUnit.Day })
            .Col("end_of_working_day", a => a.EndOfWorkingDay)
            .Col("comment", a => a.Comment)
            .Dates(a => a.CreatedAt, a => a.UpdatedAt, (a, v) => a.CreatedAt = v, (a, v) => a.UpdatedAt = v)
            .OnCreate((a, ctx) => a.Kind = kind);
    }

    private void EscalationTable(string table, string agreementColumn, ServiceLevelKind kind)
    {
        Table<ServiceLevelEscalation>(table, e => e.Id)
            .Where(e => e.Agreement!.Kind == kind)
            .Where(ctx => e => ctx.Db.Set<ServiceLevel>().Any(s => s.Id == e.Agreement!.ServiceLevelId))
            .Entity(e => e.Agreement!.ServiceLevel!.EntityId, readOnly: true)
            .Col("name", e => e.Name)
            .Col(agreementColumn, e => e.ServiceLevelAgreementId)
            .Computed("execution_time", (e, ctx) => (long)e.OffsetMinutes * 60,
                (e, v, ctx) => e.OffsetMinutes = GlpiValue.TryToLong(v, out long seconds) ? (int)(seconds / 60) : 0)
            .Col("is_active", e => e.IsActive)
            .OnCreate((e, ctx) => e.CreatedAt = Now);
    }

    /// <summary>
    /// Acteurs passés à la création ou la modification d'un objet ITIL (<c>_users_id_requester</c>,
    /// <c>_users_id_assign</c>, <c>_groups_id_assign</c>), comme CommonITILObject::prepareInputForAdd.
    /// GlpiNg n'a qu'un acteur de chaque sorte : le premier de la liste est retenu.
    /// </summary>
    private static void ApplyItilActors(GlpiInput input, Action<int?> requester, Action<int?> assignUser, Action<int?> assignGroup)
    {
        static bool TryFirst(GlpiInput input, string key, out int? id)
        {
            id = null;
            if (!input.TryGetValue(key, out JsonNode? node))
            {
                return false;
            }
            object? value = node is JsonArray array ? GlpiValue.FromJson(array.FirstOrDefault()) : GlpiValue.FromJson(node);
            id = GlpiValue.TryToLong(value, out long l) && l > 0 ? (int)l : null;
            return true;
        }

        if (TryFirst(input, "_users_id_requester", out int? r))
        {
            requester(r);
        }
        if (TryFirst(input, "_users_id_assign", out int? a))
        {
            assignUser(a);
        }
        if (TryFirst(input, "_groups_id_assign", out int? g))
        {
            assignGroup(g);
        }
    }

    /// <summary>Priorité calculée par la matrice sauf saisie explicite, dates de résolution et de clôture.</summary>
    private static void ApplyItilCommon(bool solved, bool closed, GlpiInput input, Action computePriority,
        Action<bool> setManual, bool isManual, Action<DateTime> setSolved, Action<DateTime> setClosed)
    {
        if (input.ContainsKey("priority"))
        {
            setManual(true);
        }
        else if (!isManual && (input.ContainsKey("urgency") || input.ContainsKey("impact")))
        {
            computePriority();
        }
        if (solved || closed)
        {
            setSolved(Now);
        }
        if (closed)
        {
            setClosed(Now);
        }
    }
}
