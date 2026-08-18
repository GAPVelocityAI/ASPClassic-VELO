using ASPClassic.Domain.Entities.Core;
using ASPClassic.Infrastructure.Navigation;
using ASPClassic.Infrastructure.Data;
namespace ASPClassic.Infrastructure.Data;

/// <summary>
/// Documents all database triggers present in the migrated schema so the development team
/// is aware of automatic server-side behavior that must be accounted for or replicated
/// in application logic.
/// <para>
/// <b>Legacy source:</b> New abstraction — no direct legacy equivalent. Created to centralize
/// awareness of DB-level side effects that may surprise application developers.
/// </para>
/// <para>
/// <b>Registered Triggers:</b>
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Trigger</term>
///     <description>Details</description>
///   </listheader>
///   <item>
///     <term>[portal].[TR_Navigation_RecursiveStop]</term>
///     <description>
///       <b>Timing:</b> AFTER INSERT, UPDATE on [Navigation] table.<br/>
///       <b>Purpose:</b> Prevents infinite recursion in the Navigation hierarchy by rejecting
///       any INSERT or UPDATE that would set a Navigation row's NavParentId to its own NavId
///       (self-referencing parent assignment). When triggered, the operation is rolled back
///       and an error is raised.<br/>
///       <b>Application impact:</b> Any service that inserts or updates Navigation records
///       must validate that NavParentId != NavId before saving. If the database rejects the
///       operation, the application should catch the resulting DbUpdateException and present
///       a user-friendly error message indicating that a navigation item cannot be its own parent.<br/>
///       <b>Blazor consideration:</b> Because Blazor Server uses short-lived DbContext instances
///       (via IDbContextFactory), a trigger-rejected save will surface as a DbUpdateException
///       on the SaveChangesAsync call. The NavigationTreeBuilder and any admin pages that
///       manage the navigation hierarchy should include guard logic before attempting the save.
///     </description>
///   </item>
/// </list>
/// </summary>
/// <remarks>
/// This class intentionally has no executable code. It exists solely as a compile-time
/// documentation artifact that can be discovered via IDE search, code review, or
/// architectural documentation tooling.
///
/// When new triggers are added to the database schema, add a corresponding XML doc entry
/// to this class so the team remains aware of server-side behavior.
///
/// <b>Validation pattern for NavigationTreeBuilder / admin pages:</b>
/// <code>
/// if (navigation.NavParentId.HasValue &amp;&amp; navigation.NavParentId.Value == navigation.NavId)
/// {
///     throw new InvalidOperationException(
///         "A navigation item cannot be its own parent. " +
///         "This would create an infinite recursion in the navigation hierarchy.");
/// }
/// </code>
/// </remarks>
public class TriggerRegistry
{
    // ──────────────────────────────────────────────────────────────────────
    // TRIGGER: [portal].[TR_Navigation_RecursiveStop]
    // TABLE:   [Navigation]
    // EVENT:   AFTER INSERT, UPDATE
    // ACTION:  Rolls back the transaction if the inserted/updated row has
    //          NavParentId == NavId (self-referencing parent).
    // ──────────────────────────────────────────────────────────────────────
    //
    // This class has no methods or properties by design.
    // It serves as a discoverable, searchable reference for database triggers
    // that affect application behavior.
    //
    // To check for trigger conflicts at runtime, services should validate
    // entity state before calling SaveChangesAsync rather than relying on
    // the trigger to reject the operation — this provides better UX and
    // avoids round-tripping an invalid operation to the database.
    // ──────────────────────────────────────────────────────────────────────
}
