using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SriKanth.Model.Login_Module.DTOs
{
	public class UserCreationDetails
	{
		public List<Poses> SalesPersons { get; set; }
		public List<Roles> Roles { get; set; }
		public List<Location> Locations { get; set; }
	}
	public class Poses
	{
		public string SalesPersonCode { get; set; }
		public string SalesPersonName { get; set; }
		public string Email {  get; set; }
		public string Phone { get; set; }
	}
	public class Roles
	{
		public int RoleId { get; set; }
		public string RoleName { get; set; }
	}
    public class Location
    {
        public string LocationCode {  get; set; }
		public string LocationName {  get; set; }
    }


}
