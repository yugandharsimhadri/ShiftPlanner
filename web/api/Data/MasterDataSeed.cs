namespace ShiftPlanner.Api.Data;

// Default Location/JobRole entries every new team starts with — admins can add more from
// there. Cities cover India for now, per the current customer base.
public static class MasterDataSeed
{
    public static readonly string[] Cities =
    {
        "Mumbai", "Delhi", "Bengaluru", "Hyderabad", "Ahmedabad", "Chennai", "Kolkata",
        "Surat", "Pune", "Jaipur", "Lucknow", "Kanpur", "Nagpur", "Indore", "Bhopal",
        "Visakhapatnam", "Patna", "Vadodara", "Ghaziabad", "Coimbatore",
    };

    public static readonly string[] JobRoles =
    {
        "Admin", "Manager", "Team Lead", "Executive", "Associate", "Trainee",
    };
}
