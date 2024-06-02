using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Handbrake.Models
{
    public class ConvertedFiles
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        public string? File { get; set; }
        public string? FullPath { get; set; }
    }
}
