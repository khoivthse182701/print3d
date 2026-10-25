using TheBoys.Domain.Enums;
using TheBoys.WebApi.Security;

namespace TheBoys.Tests.Unit;

public class RolePolicyTests
{
    [Fact]
    public void UserRoleEnumMaintainsAssignedIntegerValues()
    {
        Assert.Equal(0, (int)UserRole.Customer);
        Assert.Equal(2, (int)UserRole.Staff);
        Assert.Equal(3, (int)UserRole.Admin);
        Assert.Equal(4, (int)UserRole.Manager);

        // Value 1 was reserved legacy PartnerLab and must not be defined
        Assert.False(Enum.IsDefined(typeof(UserRole), 1));
    }

    [Fact]
    public void RolesConstantsAndWireNamesMatchSpecification()
    {
        Assert.Equal("cus", Roles.Customer);
        Assert.Equal("staff", Roles.Staff);
        Assert.Equal("manager", Roles.Manager);
        Assert.Equal("admin", Roles.Admin);
        Assert.Equal("staff,manager,admin", Roles.Management);

        Assert.Equal("cus", Roles.Name(UserRole.Customer));
        Assert.Equal("staff", Roles.Name(UserRole.Staff));
        Assert.Equal("manager", Roles.Name(UserRole.Manager));
        Assert.Equal("admin", Roles.Name(UserRole.Admin));
        Assert.Equal("disabled", Roles.Name((UserRole)999));
    }
}
