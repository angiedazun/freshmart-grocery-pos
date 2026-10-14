using System;
using System.Collections.Generic;
using System.Data;
using GroceryPOS.Data;
using GroceryPOS.Models;

namespace GroceryPOS.Services
{
    public static class AuthService
    {
        static UserAccount Map(DataRow r)
        {
            return new UserAccount
            {
                UserId = Convert.ToInt32(r["UserId"]),
                FullName = Convert.ToString(r["FullName"]),
                Username = Convert.ToString(r["Username"]),
                Role = Convert.ToString(r["Role"]),
                IsActive = Convert.ToBoolean(r["IsActive"]),
                PinHash = Convert.ToString(r["PinHash"])
            };
        }

        public static List<UserAccount> GetUsers(bool activeOnly)
        {
            var list = new List<UserAccount>();
            var dt = Db.Query("SELECT * FROM dbo.Users " + (activeOnly ? "WHERE IsActive=1 " : "") + "ORDER BY Role, FullName");
            foreach (DataRow r in dt.Rows) list.Add(Map(r));
            return list;
        }

        public static UserAccount Login(int userId, string pin)
        {
            var dt = Db.Query("SELECT * FROM dbo.Users WHERE UserId=@id AND IsActive=1", Db.P("@id", userId));
            if (dt.Rows.Count == 0) return null;
            var u = Map(dt.Rows[0]);
            return Security.Verify(pin, u.PinHash) ? u : null;
        }

        public static void SaveUser(UserAccount u, string newPin)
        {
            if (u.UserId == 0)
            {
                Db.Exec("INSERT INTO dbo.Users(FullName,Username,PinHash,Role,IsActive) VALUES (@n,@u,@p,@r,@a)",
                    Db.P("@n", u.FullName), Db.P("@u", u.Username), Db.P("@p", Security.HashPin(newPin)),
                    Db.P("@r", u.Role), Db.P("@a", u.IsActive));
            }
            else
            {
                Db.Exec("UPDATE dbo.Users SET FullName=@n, Username=@u, Role=@r, IsActive=@a WHERE UserId=@id",
                    Db.P("@n", u.FullName), Db.P("@u", u.Username), Db.P("@r", u.Role), Db.P("@a", u.IsActive), Db.P("@id", u.UserId));
                if (!string.IsNullOrEmpty(newPin))
                    Db.Exec("UPDATE dbo.Users SET PinHash=@p WHERE UserId=@id", Db.P("@p", Security.HashPin(newPin)), Db.P("@id", u.UserId));
            }
        }

        public static int ActiveAdminCount()
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Users WHERE Role='Admin' AND IsActive=1"));
        }
    }
}
