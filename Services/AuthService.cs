using Capableza.Web.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Capableza.Web.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly FirestoreDb _firestoreDb;
        private readonly string _firebaseWebApiKey;
        private readonly string _signInEndpoint;
        private readonly string _resetPasswordEndpoint;
        private readonly string _signUpEndpoint;
        private readonly string _updatePasswordEndpoint;

        public AuthService(HttpClient httpClient, IConfiguration configuration, FirestoreDb firestoreDb)
        {
            _httpClient = httpClient;
            _firestoreDb = firestoreDb;
            _firebaseWebApiKey = configuration["Firebase:WebApiKey"]
                ?? throw new InvalidOperationException("Firebase WebApiKey is not configured in appsettings.json or environment variables.");

            _signInEndpoint = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_firebaseWebApiKey}";
            _resetPasswordEndpoint = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_firebaseWebApiKey}";
            _signUpEndpoint = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_firebaseWebApiKey}";
            _updatePasswordEndpoint = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={_firebaseWebApiKey}";
        }

        public class FirebaseAuthResponse
        {
            public string IdToken { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public string ExpiresIn { get; set; } = string.Empty;
            public string LocalId { get; set; } = string.Empty;
        }

        public async Task<FirebaseAuthResponse?> LoginAsync(string email, string password)
        {
            var requestBody = new { email = email, password = password, returnSecureToken = true };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_signInEndpoint, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic errorData = JsonConvert.DeserializeObject(jsonResponse);
                    string errorMessage = errorData?.error?.message ?? "Login failed.";
                   
                    errorMessage = errorMessage.Replace("_", " ").ToLower();
                    if (errorMessage.Contains("invalid password") || errorMessage.Contains("email not found"))
                    {
                        errorMessage = "Invalid email or password.";
                    }
                    throw new Exception(errorMessage);
                }
                catch (JsonException)
                {
                    throw new Exception("Login failed. Check credentials.");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return JsonConvert.DeserializeObject<FirebaseAuthResponse>(jsonResponse);
        }

        public async Task SendPasswordResetEmailAsync(string email)
        {
            var requestBody = new { requestType = "PASSWORD_RESET", email = email };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_resetPasswordEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                try
                {
                    dynamic errorData = JsonConvert.DeserializeObject(jsonResponse);
                    string errorMessage = errorData?.error?.message ?? "Failed to send password reset email.";
                    throw new Exception(errorMessage.Replace("_", " ").ToLower());
                }
                catch
                {
                    throw new Exception("Failed to send password reset email. Please ensure the email address is correct.");
                }
            }
        }

        public async Task<string?> RegisterAuthUserAsync(string email, string password)
        {
            var requestBody = new { email = email, password = password, returnSecureToken = false };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_signUpEndpoint, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic errorData = JsonConvert.DeserializeObject(jsonResponse);
                    string errorMessage = errorData?.error?.message ?? "Registration failed.";
                    throw new Exception(errorMessage.Replace("_", " ").ToLower());
                }
                catch (JsonException)
                {
                    throw new Exception($"Firebase Auth registration failed. Status: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Firebase Auth registration failed: {ex.Message}");
                }
            }

            var authResponse = JsonConvert.DeserializeObject<FirebaseAuthResponse>(jsonResponse);
            if (string.IsNullOrEmpty(authResponse?.LocalId))
            {
                throw new Exception("Firebase Auth registration succeeded but did not return a user ID.");
            }
            return authResponse.LocalId;
        }

        public async Task<UserRole?> GetUserRoleAsync(string uid)
        {
            if (string.IsNullOrEmpty(uid)) return null;
            try
            {
                DocumentReference userRef = _firestoreDb.Collection("users").Document(uid);
                DocumentSnapshot snapshot = await userRef.GetSnapshotAsync();
                if (snapshot.Exists)
                {
                    var userRole = snapshot.ConvertTo<UserRole>();
                    userRole.Uid = snapshot.Id;
                    return userRole;
                }
                Console.WriteLine($"Warning: User role document not found for UID {uid}.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching user role for UID {uid}: {ex.Message}");
                return null;
            }
        }

        public async Task ChangePasswordAsync(string idToken, string newPassword)
        {
            var requestBody = new
            {
                idToken = idToken,
                password = newPassword,
                returnSecureToken = false
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_updatePasswordEndpoint, content);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    dynamic errorData = JsonConvert.DeserializeObject(jsonResponse);
                    string errorMessage = errorData?.error?.message ?? "Password change failed.";
                    throw new Exception(errorMessage.Replace("_", " ").ToLower());
                }
                catch (Exception ex)
                {
                    throw new Exception($"Password change failed: {ex.Message}");
                }
            }
        }
    }
}