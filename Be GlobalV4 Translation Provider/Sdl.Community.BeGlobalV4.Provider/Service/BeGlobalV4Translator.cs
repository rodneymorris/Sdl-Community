﻿using System;
using System.Net;
using System.Net.Cache;
using System.Text;
using System.Windows;
using Newtonsoft.Json;
using RestSharp;
using Sdl.Community.BeGlobalV4.Provider.Helpers;
using Sdl.Community.BeGlobalV4.Provider.Interfaces;
using Sdl.Community.BeGlobalV4.Provider.Model;
using Sdl.Community.BeGlobalV4.Provider.Studio;
using Sdl.LanguagePlatform.TranslationMemoryApi;
using DataFormat = RestSharp.DataFormat;

namespace Sdl.Community.BeGlobalV4.Provider.Service
{
	public class BeGlobalV4Translator
	{
		private readonly IRestClient _client;
		private readonly string _flavor;
		private const string Url = "https://translate-api.sdlbeglobal.com";
		public static readonly Log Log = Log.Instance;
		private readonly StudioCredentials _studioCredentials;
		private string _authenticationMethod = string.Empty;
		private readonly IMessageBoxService _messageBoxService;

		public BeGlobalV4Translator(BeGlobalTranslationOptions beGlobalTranslationOptions, MessageBoxService messageBoxService, TranslationProviderCredential credentials)
		{
			try
			{
				_messageBoxService = messageBoxService;
				_flavor = beGlobalTranslationOptions.Model;
				_authenticationMethod = !string.IsNullOrEmpty(beGlobalTranslationOptions.AuthenticationMethod)
					? beGlobalTranslationOptions.AuthenticationMethod
					: Constants.APICredentials;
				_studioCredentials = new StudioCredentials();
				_client = new RestClient($"{Url}/v4")
				{
					CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
				};

				if (!string.IsNullOrEmpty(_authenticationMethod))
				{
					if (_authenticationMethod.Equals(Constants.APICredentials))
					{
						var splitedCredentials = credentials?.Credential.Split('#');
						// the below condition is needed in case the ClientId is not set and credentials exists
						if (string.IsNullOrEmpty(beGlobalTranslationOptions.ClientId)
							&& splitedCredentials.Length == 2 && !string.IsNullOrEmpty(splitedCredentials[0]) && !string.IsNullOrEmpty(splitedCredentials[1]))
						{
							beGlobalTranslationOptions.ClientId = splitedCredentials[0];
							beGlobalTranslationOptions.ClientSecret = splitedCredentials[1];
							beGlobalTranslationOptions.AuthenticationMethod = _authenticationMethod;
						}
						if (!string.IsNullOrEmpty(beGlobalTranslationOptions.ClientId) && !string.IsNullOrEmpty(beGlobalTranslationOptions.ClientSecret))
						{
							var request = new RestRequest("/token", Method.POST)
							{
								RequestFormat = DataFormat.Json
							};
							request.AddBody(new { clientId = beGlobalTranslationOptions.ClientId, clientSecret = beGlobalTranslationOptions.ClientSecret });
							request.RequestFormat = DataFormat.Json;
							var response = _client.Execute(request);
							if (response.StatusCode != HttpStatusCode.OK)
							{
								throw new Exception(Constants.TokenFailed + response.Content);
							}
							dynamic json = JsonConvert.DeserializeObject(response.Content);
							_client.AddDefaultHeader("Authorization", $"Bearer {json.accessToken}");
						}
					}
					else
					{
						var accessToken = string.Empty;
						Application.Current?.Dispatcher?.Invoke(() =>
						{
							accessToken = _studioCredentials.GetToken();
						});
						accessToken = _studioCredentials.GetToken();

						if (!string.IsNullOrEmpty(accessToken))
						{
							_client.AddDefaultHeader("Authorization", $"Bearer {accessToken}");
						}
					}
				}
			}
			catch(Exception ex)
			{
				Log.Logger.Error($"{Constants.BeGlobalV4Translator} {ex.Message}\n {ex.StackTrace}");
			}
		}

		public string TranslateText(string text, string sourceLanguage, string targetLanguage)
		{
			try
			{
				var request = new RestRequest("/mt/translations/async", Method.POST)
				{
					RequestFormat = DataFormat.Json
				};
				var traceId = GetTraceId(request);

				string[] texts = { text };
				request.AddBody(new
				{
					input = texts,
					sourceLanguageId = sourceLanguage,
					targetLanguageId = targetLanguage,
					model = _flavor,
					inputFormat = "xliff"
				});
				var response = _client.Execute(request);

				if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_authenticationMethod)
					&& _authenticationMethod.Equals(Constants.StudioAuthentication))
				{
					// Get refresh token
					var token = _studioCredentials.EnsureValidConnection();
					if (!string.IsNullOrEmpty(token))
					{
						UpdateRequestHeadersForRefreshToken(request, token);

						var translationAsyncResponse = _client.Execute(request);

						if (translationAsyncResponse.StatusCode == HttpStatusCode.Unauthorized)
						{
							_messageBoxService.ShowMessage(Constants.UnauthorizedCredentials, Constants.PluginName);							
							Log.Logger.Error($"{Constants.UnauthorizedToken} {traceId}");
						}
						else if (!translationAsyncResponse.IsSuccessful && translationAsyncResponse.StatusCode != HttpStatusCode.Unauthorized)
						{
							ShowErrors(translationAsyncResponse);
						}
						return ReturnTranslation(translationAsyncResponse);
					}
				}
				else if (!response.IsSuccessful && response.StatusCode != HttpStatusCode.Unauthorized)
				{
					ShowErrors(response);
				}
				return ReturnTranslation(response);
			}
			catch (Exception e)
			{
				Log.Logger.Error($"{Constants.TranslateTextMethod} {e.Message}\n {e.StackTrace}");
			}
			return string.Empty;
		}

		private string ReturnTranslation(IRestResponse response)
		{
			dynamic json = JsonConvert.DeserializeObject(response.Content);

			var rawData = WaitForTranslation(json?.requestId?.Value);

			json = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(rawData));
			return json != null ? json.translation[0] : string.Empty;
		}

		public int GetUserInformation()
		{
			try
			{
				RestRequest request;
				if (!string.IsNullOrEmpty(_authenticationMethod) && _authenticationMethod.Equals(Constants.APICredentials))
				{
					request = new RestRequest("/accounts/api-credentials/self")
					{
						RequestFormat = DataFormat.Json
					};
				}
				else
				{
					request = new RestRequest("/accounts/users/self")
					{
						RequestFormat = DataFormat.Json
					};
				}
				var traceId = GetTraceId(request);
				var response = _client.Execute(request);
				var user = JsonConvert.DeserializeObject<UserDetails>(response.Content);
				if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_authenticationMethod)
					&& _authenticationMethod.Equals(Constants.StudioAuthentication))
				{
					// Get refresh token
					var token = _studioCredentials.EnsureValidConnection();
					if (!string.IsNullOrEmpty(token))
					{
						// Update authorization parameters
						UpdateRequestHeadersForRefreshToken(request, token);

						var userInfoResponse = _client.Execute(request);
						if (userInfoResponse.StatusCode == HttpStatusCode.OK)
						{
							return JsonConvert.DeserializeObject<UserDetails>(userInfoResponse.Content).AccountId;
						}
						if (userInfoResponse.StatusCode == HttpStatusCode.Unauthorized)
						{
							_messageBoxService.ShowMessage(Constants.UnauthorizedCredentials, Constants.PluginName);
							Log.Logger.Error($"{Constants.UnauthorizedUserInfo} {traceId}");
						}
						else if (userInfoResponse.StatusCode != HttpStatusCode.OK && userInfoResponse.StatusCode != HttpStatusCode.Unauthorized)
						{
							ShowErrors(userInfoResponse);
						}
					}
				}
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					_messageBoxService.ShowMessage(Constants.UnauthorizedCredentials, Constants.PluginName);
					Log.Logger.Error($"{Constants.UnauthorizedUserInfo} {traceId}");
				}
				else if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Unauthorized)
				{
					ShowErrors(response);
				}

				return user?.AccountId ?? 0;
			}
			catch(Exception ex)
			{
				Log.Logger.Error($"{Constants.GetUserInformation} {ex.Message}\n {ex.StackTrace}");
			}
			return 0;
		}

		private void UpdateRequestHeadersForRefreshToken(IRestRequest request, string token)
		{
			// Update authorization parameters
			_client.RemoveDefaultParameter("Authorization");
			_client.AddDefaultHeader("Authorization", $"Bearer {token}");
			request.AddOrUpdateParameter("Authorization", $"Bearer {token}");
		}
		public SubscriptionInfo GetLanguagePairs(string accountId)
		{
			try
			{
				var request = new RestRequest($"/accounts/{accountId}/subscriptions/language-pairs")
				{
					RequestFormat = DataFormat.Json
				};
				var traceId =GetTraceId(request);

				var response = _client.Execute(request);
				if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_authenticationMethod)
				&& _authenticationMethod.Equals(Constants.StudioAuthentication))
				{
					// Get refresh token
					var token = _studioCredentials.EnsureValidConnection();
					if (!string.IsNullOrEmpty(token))
					{
						// Update authorization parameters
						UpdateRequestHeadersForRefreshToken(request, token);
						var languagePairsResponse = _client.Execute(request);

						if (languagePairsResponse.StatusCode == HttpStatusCode.OK)
						{
							return JsonConvert.DeserializeObject<SubscriptionInfo>(languagePairsResponse.Content); 
						}
						if (languagePairsResponse.StatusCode == HttpStatusCode.Unauthorized)
						{
							_messageBoxService.ShowMessage(Constants.UnauthorizedCredentials, Constants.PluginName);
							Log.Logger.Error($"{Constants.UnauthorizedLanguagePairs} {traceId}");
						}
						else if (languagePairsResponse.StatusCode != HttpStatusCode.OK && languagePairsResponse.StatusCode != HttpStatusCode.Unauthorized)
						{
							ShowErrors(languagePairsResponse);
						}
					}
				}
				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					_messageBoxService.ShowMessage(Constants.UnauthorizedCredentials, Constants.PluginName);
					Log.Logger.Error($"{Constants.UnauthorizedUserInfo} {traceId}");
				}
				else if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.Unauthorized)
				{
					ShowErrors(response);
				}
				var subscriptionInfo = JsonConvert.DeserializeObject<SubscriptionInfo>(response.Content);
				return subscriptionInfo;
			}
			catch (Exception e)
			{
				Log.Logger.Error($"{Constants.SubscriptionInfoMethod} {e.Message}\n {e.StackTrace}");
			}
			return new SubscriptionInfo();
		}

		private byte[] WaitForTranslation(string id)
		{
			try
			{
				IRestResponse response;
				string status;
				do
				{
					response = RestGet($"/mt/translations/async/{id}");
					if (!response.IsSuccessful || response.StatusCode != HttpStatusCode.OK)
					{
						ShowErrors(response);
					}

					dynamic json = JsonConvert.DeserializeObject(response.Content);
					status = json.translationStatus;

					if (!status.Equals(Constants.DONE, StringComparison.CurrentCultureIgnoreCase))
					{
						System.Threading.Thread.Sleep(300);
					}
					if (status.Equals(Constants.FAILED))
					{
						ShowErrors(response);
					}
				} while (status.Equals(Constants.INIT, StringComparison.CurrentCultureIgnoreCase) ||
				         status.Equals(Constants.TRANSLATING, StringComparison.CurrentCultureIgnoreCase));

				response = RestGet($"/mt/translations/async/{id}/content");
				if (!response.IsSuccessful || response.StatusCode != HttpStatusCode.OK)
				{
					ShowErrors(response);
				}
				return response.RawBytes;
			}
			catch (Exception e)
			{
				Log.Logger.Error($"{Constants.WaitTranslationMethod} {e.Message}\n {e.StackTrace}");
			}
			return new byte[1];
		}

		private IRestResponse RestGet(string command)
		{
			var request = new RestRequest(command)
			{
				RequestFormat = DataFormat.Json
			};
			GetTraceId(request);

			var response = _client.Execute(request);
			return response;
		}

		private string GetTraceId(IRestRequest request)
		{
			var pluginVersion = VersionHelper.GetPluginVersion();
			var studioVersion = VersionHelper.GetStudioVersion();
			var traceId = $"{Constants.SDLMachineTranslationCloud} {pluginVersion} - {studioVersion} - {Guid.NewGuid().ToString()}";
			request.AddHeader(Constants.TraceId, traceId);
			return traceId;
		}

		private void ShowErrors(IRestResponse response)
		{
			var responseContent = JsonConvert.DeserializeObject<ResponseError>(response.Content);

			if (response.StatusCode == HttpStatusCode.Forbidden)
			{
				_messageBoxService.ShowMessage(Constants.ForbiddenLicense, Constants.PluginName);
				throw new Exception(Constants.ForbiddenLicense);
			}
			if (responseContent?.Errors != null)
			{
				foreach (var error in responseContent.Errors)
				{
					throw new Exception($"{Constants.ErrorCode} {error.Code}, {error.Description}");
				}
			}
		}		
	}
}