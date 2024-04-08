using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Web.Script.Serialization;

namespace Json測試
{
    public class Json
    {
        public static string DataTable_To_Json(DataTable input) 
        {
            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append("[");
            if (input != null && input.Rows.Count > 0)
            {
                foreach (DataRow row in input.Rows)
                {
                    jsonBuilder.Append("{");
                    foreach (DataColumn col in input.Columns)
                    {
                        string type = col.DataType.Name;
                        if (type == "DateTime" || type == "String")
                        {
                            jsonBuilder.Append("\"" + col.ColumnName + "\":\"" + row[col.ColumnName] + "\",");
                        }
                        else
                        {
                            string value = row[col.ColumnName].ToString();
                            if (value != "")
                            {
                                jsonBuilder.Append("\"" + col.ColumnName + "\":" + row[col.ColumnName] + ",");
                            }
                            else
                            {
                                jsonBuilder.Append("\"" + col.ColumnName + "\":null,");
                            }
                        }
                    }
                    jsonBuilder.Remove(jsonBuilder.Length - 1, 1);
                    jsonBuilder.Append("},");
                }
                jsonBuilder.Remove(jsonBuilder.Length - 1, 1);
            }
            jsonBuilder.Append("]");

            return jsonBuilder.ToString();
        }

        public static DataTable Json_To_DataTable(string json)
        {
            DataTable dataTable = new DataTable();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            dynamic jsonObject = serializer.Deserialize<dynamic>(json);

            foreach (var item in jsonObject)
            {
                DataRow row = dataTable.NewRow();
                foreach (var kvp in item)
                {
                    if (!dataTable.Columns.Contains(kvp.Key))
                    {
                        if (kvp.Value is string)
                        {
                            dataTable.Columns.Add(kvp.Key, typeof(String));
                        }
                        else if (kvp.Value is decimal)
                        {
                            dataTable.Columns.Add(kvp.Key, typeof(Decimal));
                        }
                        else
                        {
                            dataTable.Columns.Add(kvp.Key, typeof(Int32));
                        }
                    }

                    if (kvp.Value != null)
                    {
                        row[kvp.Key] = kvp.Value;
                    }
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }
}
