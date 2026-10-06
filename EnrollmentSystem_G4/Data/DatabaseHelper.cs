using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace EnrollmentSystem_G4.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        // Helper 1: Execute SELECT queries that return multiple records
        public DataTable ExecuteQuery(string query, Dictionary<string, object>? parameters = null)
        {
            using (var connection = new MySqlConnection(_connectionString))
            using (var command = new MySqlCommand(query, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                using (var adapter = new MySqlDataAdapter(command))
                {
                    var dataTable = new DataTable();
                    adapter.Fill(dataTable);
                    return dataTable;
                }
            }
        }

        // Helper 2: Execute INSERT, UPDATE, DELETE commands (returns affected rows count)
        public int ExecuteNonQuery(string commandText, Dictionary<string, object>? parameters = null)
        {
            using (var connection = new MySqlConnection(_connectionString))
            using (var command = new MySqlCommand(commandText, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        // Helper 3: Execute queries that return a single value (e.g., LAST_INSERT_ID() or COUNT())
        public object? ExecuteScalar(string commandText, Dictionary<string, object>? parameters = null)
        {
            using (var connection = new MySqlConnection(_connectionString))
            using (var command = new MySqlCommand(commandText, connection))
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }

                connection.Open();
                return command.ExecuteScalar();
            }
        }
    }
}