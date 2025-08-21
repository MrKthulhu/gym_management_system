using Npgsql;

namespace gym_management_system.Data;

public static class Db
{
    private static string _cs = "";
    public static void Init(string connectionString) => _cs = connectionString;

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(_cs);
        await c.OpenAsync();
        return c;
    }

    // Shapes 
    public sealed record PlanRow(string Id, string Name, int DurationMonths, int PriceCents);
    public sealed record TrainerRow(string Id, string FirstName, string? LastName, string? Specialization);
    public sealed record RegisterResult(string UserId, string MembershipId, string PaymentId);
    public sealed record MemberRow(
        string Id,
        string FirstName,
        string? LastName,
        int Age,
        string Email,
        string? PlanName,
        int? PriceCents,
        string? MembershipStatus,
        DateTime? StartDate,
        DateTime? EndDate
    );
    public sealed record TrainerAssignmentRow(
        string TrainerId,
        string TrainerFirstName,
        string? TrainerLastName,
        string? Specialization,
        string MemberId,
        string MemberFirstName,
        string? MemberLastName,
        string MemberEmail
);


    public sealed record TodayAttendanceRow(string FirstName, string? LastName, string Email, DateTime AttendanceAtUtc);
    public sealed record MarkAttendanceResult(string SessionId, bool AlreadyMarked);

    // Trainers (now its own table, was having issue's displaying overall members)
    public static async Task<List<TrainerRow>> GetTrainersAsync()
    {
        const string sql = """
            select
                t.id               as "Id",
                t."firstName"      as "FirstName",
                t."lastName"       as "LastName",
                t."specialization" as "Specialization"
            from "Trainer" t
            order by lower(t."firstName"), lower(coalesce(t."lastName", ''));
        """;

        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<TrainerRow>();
        while (await r.ReadAsync())
        {
            var id = r.GetString(r.GetOrdinal("Id"));
            var fn = r.GetString(r.GetOrdinal("FirstName"));
            string? ln = r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName"));
            string? spc = r.IsDBNull(r.GetOrdinal("Specialization")) ? null : r.GetString(r.GetOrdinal("Specialization"));
            list.Add(new TrainerRow(id, fn, ln, spc));
        }
        return list;
    }

    public static async Task<string> AddTrainerAsync(string fullName, string specialization)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.ElementAtOrDefault(0) ?? fullName;
        var last = parts.ElementAtOrDefault(1);
        var id = Guid.NewGuid().ToString("N");

        await using var conn = await OpenAsync();
        var cmd = new NpgsqlCommand("""
            INSERT INTO "Trainer" ("id","firstName","lastName","specialization","createdAt","updatedAt")
            VALUES (@id,@fn,@ln,@spec,now(),now())
        """, conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@fn", first);
        cmd.Parameters.AddWithValue("@ln", (object?)last ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@spec", specialization);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }
        
    // Member -> Trainer link (FK now points to Trainer.id same call-site API)
    public static async Task AssignTrainerAsync(string memberEmail, string trainerId)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var memberId = (string?)await new NpgsqlCommand("""SELECT "id" FROM "User" WHERE "email"=@em""", conn, tx)
        { Parameters = { new("@em", memberEmail) } }.ExecuteScalarAsync()
        ?? throw new Exception("Member not found");

        var upd = new NpgsqlCommand("""UPDATE "User" SET "trainerId"=@tid, "updatedAt"=now() WHERE "id"=@mid""", conn, tx);
        upd.Parameters.AddWithValue("@tid", trainerId);
        upd.Parameters.AddWithValue("@mid", memberId);
        await upd.ExecuteNonQueryAsync();

        await tx.CommitAsync();
    }

    // Plans
    public static async Task<List<PlanRow>> GetPlansAsync()
    {
        const string sql = """
            SELECT "id","name","durationMonths","priceCents"
            FROM "Plan" WHERE "isActive" = true ORDER BY "priceCents";
        """;
        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<PlanRow>();
        while (await r.ReadAsync())
            list.Add(new PlanRow(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        return list;
    }

    // Members (latest membership + plan)

    public static async Task<List<MemberRow>> GetMembersAsync()
    {
        const string sql = """
            select
                u.id                               as "Id",
                u."firstName"                      as "FirstName",
                u."lastName"                       as "LastName",
                u.age                              as "Age",
                u.email                            as "Email",
                p.name                             as "PlanName",
                p."priceCents"                     as "PriceCents",
                m.status::text                     as "MembershipStatus",
                m."startDate"                      as "StartDate",
                m."endDate"                        as "EndDate"
            from "User" u
            left join lateral (
                select m.*
                from "Membership" m
                where m."userId" = u.id
                order by m."startDate" desc
                limit 1
            ) m on true
            left join "Plan" p on p.id = m."planId"
            order by lower(u."firstName"), lower(coalesce(u."lastName", ''));
        """;

        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<MemberRow>();
        while (await r.ReadAsync())
        {
            string id = r.GetString(r.GetOrdinal("Id"));
            string firstName = r.GetString(r.GetOrdinal("FirstName"));
            string? lastName = r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName"));
            int age = r.GetInt32(r.GetOrdinal("Age"));
            string email = r.GetString(r.GetOrdinal("Email"));
            string? planName = r.IsDBNull(r.GetOrdinal("PlanName")) ? null : r.GetString(r.GetOrdinal("PlanName"));
            int? priceCents = r.IsDBNull(r.GetOrdinal("PriceCents")) ? (int?)null : r.GetInt32(r.GetOrdinal("PriceCents"));
            string? status = r.IsDBNull(r.GetOrdinal("MembershipStatus")) ? null : r.GetString(r.GetOrdinal("MembershipStatus"));
            DateTime? started = r.IsDBNull(r.GetOrdinal("StartDate")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("StartDate"));
            DateTime? ends = r.IsDBNull(r.GetOrdinal("EndDate")) ? (DateTime?)null : r.GetDateTime(r.GetOrdinal("EndDate"));

            list.Add(new MemberRow(id, firstName, lastName, age, email, planName, priceCents, status, started, ends));
        }

        return list;
    }

    // Register Member (membership +payment)
    public static async Task<RegisterResult> RegisterMemberAsync(string fullName, int age, string email, string planId)
    {
        var parts = (fullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : fullName;
        var last = parts.Length > 1 ? parts[1] : null;

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // plan
        var planCmd = new NpgsqlCommand("""SELECT "durationMonths","priceCents" FROM "Plan" WHERE "id"=@pid""", conn, tx);
        planCmd.Parameters.AddWithValue("@pid", planId);
        await using var pr = await planCmd.ExecuteReaderAsync();
        if (!await pr.ReadAsync()) throw new Exception("Plan not found.");
        var duration = pr.GetInt32(0);
        var price = pr.GetInt32(1);
        await pr.DisposeAsync();

        string userId;
        {
            var tryGet = new NpgsqlCommand("""SELECT "id" FROM "User" WHERE "email"=@em""", conn, tx);
            tryGet.Parameters.AddWithValue("@em", email);
            var existing = (string?)await tryGet.ExecuteScalarAsync();

            if (existing is null)
            {
                userId = Guid.NewGuid().ToString("N");
                var ins = new NpgsqlCommand("""
                    INSERT INTO "User" 
                    ("id","email","password","role","firstName","lastName","age","createdAt","updatedAt")
                    VALUES (@id,@em,NULL,'MEMBER',@fn,@ln,@age,now(),now())
                """, conn, tx);
                ins.Parameters.AddWithValue("@id", userId);
                ins.Parameters.AddWithValue("@em", email);
                ins.Parameters.AddWithValue("@fn", first);
                ins.Parameters.AddWithValue("@ln", (object?)last ?? DBNull.Value);
                ins.Parameters.AddWithValue("@age", age);
                await ins.ExecuteNonQueryAsync();
            }
            else userId = existing;
        }

        // ensure no active membership
        var activeCountCmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM "Membership" 
            WHERE "userId"=@uid AND ("endDate" IS NULL OR "endDate" > now())
        """, conn, tx);
        activeCountCmd.Parameters.AddWithValue("@uid", userId);
        var active = (long)(await activeCountCmd.ExecuteScalarAsync() ?? 0);
        if (active > 0) throw new Exception("User already has an active membership.");

        // create membership + payment
        var membershipId = Guid.NewGuid().ToString("N");
        var start = DateTime.UtcNow;
        var end = start.AddMonths(duration);

        var m = new NpgsqlCommand("""
            INSERT INTO "Membership" ("id","userId","planId","startDate","endDate","createdAt","updatedAt")
            VALUES (@id,@uid,@pid,@start,@end,now(),now())
        """, conn, tx);
        m.Parameters.AddWithValue("@id", membershipId);
        m.Parameters.AddWithValue("@uid", userId);
        m.Parameters.AddWithValue("@pid", planId);
        m.Parameters.AddWithValue("@start", start);
        m.Parameters.AddWithValue("@end", end);
        await m.ExecuteNonQueryAsync();

        var paymentId = Guid.NewGuid().ToString("N");
        var p = new NpgsqlCommand("""
            INSERT INTO "Payment" ("id","membershipId","amountCents","currencyCode","status","createdAt")
            VALUES (@id,@mid,@amt,'CAD','PENDING',now())
        """, conn, tx);
        p.Parameters.AddWithValue("@id", paymentId);
        p.Parameters.AddWithValue("@mid", membershipId);
        p.Parameters.AddWithValue("@amt", price);
        await p.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return new RegisterResult(userId, membershipId, paymentId);
    }

    // Attendance / Sessions
    public static async Task<string> MarkAttendanceAsync(string memberEmail, string title, DateTime startAt)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var uid = (string?)await new NpgsqlCommand("""SELECT "id" FROM "User" WHERE "email"=@em""", conn, tx)
        { Parameters = { new("@em", memberEmail) } }.ExecuteScalarAsync()
        ?? throw new Exception("Member not found");

        var sessionId = Guid.NewGuid().ToString("N");
        var s = new NpgsqlCommand("""INSERT INTO "Session" ("id","trainerId","startAt","title","createdAt") VALUES (@id,NULL,@start,@title,now())""", conn, tx);
        s.Parameters.AddWithValue("@id", sessionId);
        s.Parameters.AddWithValue("@start", startAt);
        s.Parameters.AddWithValue("@title", (object?)title ?? DBNull.Value);
        await s.ExecuteNonQueryAsync();

        var a = new NpgsqlCommand("""INSERT INTO "Attendance" ("id","userId","sessionId","createdAt") VALUES (@id,@uid,@sid,now())""", conn, tx);
        a.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
        a.Parameters.AddWithValue("@uid", uid);
        a.Parameters.AddWithValue("@sid", sessionId);
        await a.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return sessionId;
    }
    
    public static async Task<List<TrainerAssignmentRow>> GetTrainerAssignmentsAsync(bool onlyActive = true) // Trainer assignment
    {
        const string sql = """
        select
            t.id                as "TrainerId",
            t."firstName"       as "TrainerFirstName",
            t."lastName"        as "TrainerLastName",
            t."specialization"  as "Specialization",
            u.id                as "MemberId",
            u."firstName"       as "MemberFirstName",
            u."lastName"        as "MemberLastName",
            u.email             as "MemberEmail"
        from "Trainer" t
        join "User" u on u."trainerId" = t.id
        left join lateral (
            select m.status
            from "Membership" m
            where m."userId" = u.id
            order by m."startDate" desc
            limit 1
        ) m on true
        where (not @onlyActive) or (m.status = 'ACTIVE')
        order by lower(t."firstName"), lower(coalesce(t."lastName", '')),
                 lower(u."firstName"), lower(coalesce(u."lastName", ''));
    """;

        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@onlyActive", onlyActive);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<TrainerAssignmentRow>();
        while (await r.ReadAsync())
        {
            list.Add(new TrainerAssignmentRow(
                r.GetString(r.GetOrdinal("TrainerId")),
                r.GetString(r.GetOrdinal("TrainerFirstName")),
                r.IsDBNull(r.GetOrdinal("TrainerLastName")) ? null : r.GetString(r.GetOrdinal("TrainerLastName")),
                r.IsDBNull(r.GetOrdinal("Specialization")) ? null : r.GetString(r.GetOrdinal("Specialization")),
                r.GetString(r.GetOrdinal("MemberId")),
                r.GetString(r.GetOrdinal("MemberFirstName")),
                r.IsDBNull(r.GetOrdinal("MemberLastName")) ? null : r.GetString(r.GetOrdinal("MemberLastName")),
                r.GetString(r.GetOrdinal("MemberEmail"))
            ));
        }
        return list;
    }


    // Marks attendance for today's session (per trainer, per day)
    public static async Task<MarkAttendanceResult> MarkAttendanceTodayAsync(string memberEmail, DateTime nowLocal)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");

        // Local day window -> UTC
        var localStart = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var endUtc = startUtc.AddDays(1);

        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Resolve user
        var getUser = new NpgsqlCommand("""SELECT u.id, u."trainerId" FROM "User" u WHERE u.email = @em""", conn, tx);
        getUser.Parameters.AddWithValue("@em", memberEmail);
        await using var ur = await getUser.ExecuteReaderAsync();
        if (!await ur.ReadAsync()) throw new Exception("Member not found.");
        var userId = ur.GetString(0);
        var trainerId = ur.IsDBNull(1) ? null : ur.GetString(1);
        await ur.DisposeAsync();

        // Ensure latest membership is ACTIVE
        var mem = new NpgsqlCommand("""
        select m.status::text
        from "Membership" m
        where m."userId" = @uid
        order by m."startDate" desc
        limit 1
    """, conn, tx);
        mem.Parameters.AddWithValue("@uid", userId);
        var status = (string?)await mem.ExecuteScalarAsync() ?? "";
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Member does not have an ACTIVE membership.");

        // Find today's session for this trainer (per-day session)
        string? sessionId = null;
        {
            var find = new NpgsqlCommand("""
            select s.id
            from "Session" s
            where s."startAt" >= @start and s."startAt" < @end
              and ((@tid is null and s."trainerId" is null) or s."trainerId" = @tid)
            limit 1
        """, conn, tx);
            find.Parameters.AddWithValue("@start", startUtc);
            find.Parameters.AddWithValue("@end", endUtc);
            find.Parameters.AddWithValue("@tid", (object?)trainerId ?? DBNull.Value);
            sessionId = (string?)await find.ExecuteScalarAsync();
        }

        // Create if missing
        if (sessionId is null)
        {
            sessionId = Guid.NewGuid().ToString("N");
            var ins = new NpgsqlCommand("""
            insert into "Session" ("id","trainerId","startAt","title","createdAt")
            values (@id,@tid,@start,'Daily Session', now())
        """, conn, tx);
            ins.Parameters.AddWithValue("@id", sessionId);
            ins.Parameters.AddWithValue("@tid", (object?)trainerId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@start", startUtc);
            await ins.ExecuteNonQueryAsync();
        }

        // Check duplicate
        var exists = new NpgsqlCommand("""
        select 1 from "Attendance" a where a."userId"=@uid and a."sessionId"=@sid
    """, conn, tx);
        exists.Parameters.AddWithValue("@uid", userId);
        exists.Parameters.AddWithValue("@sid", sessionId);
        var dup = await exists.ExecuteScalarAsync() != null;

        if (dup)
        {
            await tx.CommitAsync();
            return new MarkAttendanceResult(sessionId, AlreadyMarked: true);
        }

        // Insert attendance
        var a = new NpgsqlCommand("""
        insert into "Attendance" ("id","userId","sessionId","createdAt")
        values (@id,@uid,@sid, now())
    """, conn, tx);
        a.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
        a.Parameters.AddWithValue("@uid", userId);
        a.Parameters.AddWithValue("@sid", sessionId);
        await a.ExecuteNonQueryAsync();

        await tx.CommitAsync();
        return new MarkAttendanceResult(sessionId, AlreadyMarked: false);
    }

    // Returns all attendance for today's sessions (any trainer), newest first
    public static async Task<List<TodayAttendanceRow>> GetTodayAttendanceAsync(DateTime nowLocal)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Edmonton");
        var localStart = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var endUtc = startUtc.AddDays(1);

        const string sql = """
        select u."firstName" as "FirstName",
               u."lastName"  as "LastName",
               u.email       as "Email",
               a."createdAt" as "AttendanceAtUtc"
        from "Attendance" a
        join "User" u on u.id = a."userId"
        join "Session" s on s.id = a."sessionId"
        where s."startAt" >= @start and s."startAt" < @end
        order by a."createdAt" desc
        limit 200
    """;

        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@start", startUtc);
        cmd.Parameters.AddWithValue("@end", endUtc);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<TodayAttendanceRow>();
        while (await r.ReadAsync())
        {
            list.Add(new TodayAttendanceRow(
                r.GetString(r.GetOrdinal("FirstName")),
                r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName")),
                r.GetString(r.GetOrdinal("Email")),
                r.GetDateTime(r.GetOrdinal("AttendanceAtUtc"))
            ));
        }
        return list;
    }

    // Returns recent attendance (global), newest first
    public sealed record RecentAttendanceRow(string FirstName, string? LastName, string Email, DateTime AttendanceAtUtc);

    public static async Task<List<RecentAttendanceRow>> GetRecentAttendanceAsync(int limit)
    {
        const string sql = """
        select u."firstName" as "FirstName",
               u."lastName"  as "LastName",
               u.email       as "Email",
               a."createdAt" as "AttendanceAtUtc"
        from "Attendance" a
        join "User" u on u.id = a."userId"
        order by a."createdAt" desc
        limit @lim
    """;

        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@lim", limit);
        await using var r = await cmd.ExecuteReaderAsync();

        var list = new List<RecentAttendanceRow>();
        while (await r.ReadAsync())
        {
            list.Add(new RecentAttendanceRow(
                r.GetString(r.GetOrdinal("FirstName")),
                r.IsDBNull(r.GetOrdinal("LastName")) ? null : r.GetString(r.GetOrdinal("LastName")),
                r.GetString(r.GetOrdinal("Email")),
                r.GetDateTime(r.GetOrdinal("AttendanceAtUtc"))
            ));
        }
        return list;
    }







    // Helpers 

    public static string Money(int cents) =>
        (cents / 100.0m).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-CA"));

    public static async Task UnassignTrainerAsync(string memberEmail)
    {
        await using var conn = await OpenAsync();
        var cmd = new NpgsqlCommand("""UPDATE "User" SET "trainerId" = NULL, "updatedAt" = now() WHERE "email" = @em""", conn);
        cmd.Parameters.AddWithValue("@em", memberEmail);
        var n = await cmd.ExecuteNonQueryAsync();
        if (n == 0) throw new Exception("Member not found.");
    }
}
