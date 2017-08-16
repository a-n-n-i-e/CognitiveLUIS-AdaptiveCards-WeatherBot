using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Net.Http;
using Newtonsoft.Json;
using AdaptiveCards;
using WeatherBot.Models; // <projectname>.Models
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;


namespace WeatherBot.Dialogs
{
    [LuisModel("APP_ID", "ENDPOINT_KEY")]
    [Serializable]
    //public class RootDialog : IDialog<object>
    public class RootDialog : LuisDialog<object>
    {
        //public Task StartAsync(IDialogContext context)
        //{
        //    context.Wait(MessageReceivedAsync);
        //    return Task.CompletedTask;
        //}

        [LuisIntent("")]
        [LuisIntent("None")]
        private async Task None (IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"日本の都市の天気予報(今日から3日間)を調べる、天気予報Botです。");
            context.Done<object>(null);
        }

        [LuisIntent("GetWeather")]
        private async Task GetWeatherAsync(IDialogContext context, LuisResult result)
        {
            var selectedDay = "";
            var cityName = "";
            var cityId = "";

            // LUIS の判定結果から Entity を取得 | Get Entities from LUIS result
            foreach (var entity in result.Entities)
            {
                if (entity.Type == "Place")
                {
                    cityName = entity.Entity.ToString();

                }
                else if (entity.Type.Substring(0, 3) == "Day")
                {
                    selectedDay = entity.Type.Substring(5);
                }
            }

            // 都市名から都市IDを取得 | Get CityId from place
            cityId = await GetLocationAsync(cityName);

            if (cityId == "")
            {
                await context.PostAsync($"ゴメンナサイ、分からなかったです。日本の都市名を入れてね。");
                context.Done<object>(null);
            }
            else
            {

                // 天気を取得 | Get Weather
                WeatherModel weather = await GetWeatherAsync(cityId);

                // 取得した天気情報をカードにセット | Set weather to card
                var weatherCard = GetCard(weather, selectedDay);
                var attachment = new Attachment()
                {
                    Content = weatherCard,
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Name = "Weather Forecast"
                };

                //　返答メッセージにカードを添付 | Add card to message
                var message = context.MakeMessage();
                message.Attachments.Add(attachment);

                //　返答メッセージをPost | Post message
                await context.PostAsync(message);
                context.Done<object>(null);

            }
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            // 返答メッセージを作成 | Create return message to user
            var message = context.MakeMessage();
            // 天気を取得 | Get weather forecast
            WeatherModel weather = await GetWeatherAsync();

            //// 天気をメッセージにセット | Set weather into message
            //message.Text = $"今日の天気は {weather.forecasts[0].telop.ToString()} です";

            // 取得した天気情報をカードにセット | Set weather to card
            var weatherCard = GetCard(weather);
            var attachment = new Attachment()
            {
                Content = weatherCard,
                ContentType = "application/vnd.microsoft.card.adaptive",
                Name = "Weather Forecast"
            };

            //　返答メッセージにカードを添付 | Add card to message
            message.Attachments.Add(attachment);

            //　返答メッセージをPost | Post message
            await context.PostAsync(message);
            context.Wait(MessageReceivedAsync);
        }

        private async Task<string> GetLocationAsync(string place)
        {
            var client = new HttpClient();
            var locationResult = await client.GetStringAsync("https://raw.githubusercontent.com/a-n-n-i-e/CognitiveLUIS-AdaptiveCards-WeatherBot/master/WeatherBot/locationIdList.json");
            var locationStr = Uri.UnescapeDataString(locationResult.ToString());
            var locationModel = JsonConvert.DeserializeObject<LocationModel>(locationStr);

            var locationId = "";

            // 都市名に一致するコードを取得 | Get CityId that matches place
            foreach (var location in locationModel.locations)
            {
                if (location.city == place)
                {
                    locationId = location.city_id;
                    break;
                }
            }

            // 取得できない場合は都道府県名を確認 | When can't get from city list, check pref list
            if (locationId == "")
            {
                foreach (var location in locationModel.locations)
                {
                    if (location.pref.Trim(new char[] { '都', '府', '県' }) == place)
                    {
                        locationId = location.city_id;
                        break;
                    }
                }
            }
            return locationId;
        }



        private async Task<WeatherModel> GetWeatherAsync()
        {
            // API から天気情報を取得 (都市コード 140010 (横浜) の場合) | Get weather forecast from API (cityid=140010 (Yokohama)
            var client = new HttpClient();
            var weatherResult = await client.GetStringAsync("http://weather.livedoor.com/forecast/webservice/json/v1?city=140010");

            // API 取得したデータをデコードして WeatherModel に取得 | Parse weather data into WeatherModel
            weatherResult = Uri.UnescapeDataString(weatherResult);
            var weatherModel = JsonConvert.DeserializeObject<WeatherModel>(weatherResult);
            return weatherModel;
        }

        private async Task<WeatherModel> GetWeatherAsync(string cityId)
        {
            // API から天気情報を取得
            var client = new HttpClient();
            var result = await client.GetStringAsync("http://weather.livedoor.com/forecast/webservice/json/v1?city=" + cityId);

            // API 取得したデータをデコードして WeatherModel に取得 | Parse weather data into WeatherModel
            result = Uri.UnescapeDataString(result);
            var model = JsonConvert.DeserializeObject<WeatherModel>(result);
            return model;

        }

        private static AdaptiveCard GetCard(WeatherModel model)
        {
            var card = new AdaptiveCard();
            //AddCurrentWeather(model, card);
            AddWeather(model, card);
            return card;
        }
        private static AdaptiveCard GetCard(WeatherModel model, string day)
        {
            var card = new AdaptiveCard();
            AddWeather(model, card, day);
            return card;
        }

        private static void AddTextBlock(Column column, string text, TextSize size, HorizontalAlignment alignment)
        {
            column.Items.Add(new TextBlock()
            {
                Text = text,
                Size = size,
                HorizontalAlignment = alignment
            });
        }
        private static void AddImage(Column column, string url, ImageSize size, HorizontalAlignment alignment)
        {
            column.Items.Add(new AdaptiveCards.Image()
            {
                Url = url,
                Size = size,
                HorizontalAlignment = alignment
            });
        }

        private static void AddWeather(WeatherModel model, AdaptiveCard card)
        {
            // タイトル作成 | Create title of card
            var titleColumnSet = new ColumnSet();
            card.Body.Add(titleColumnSet);

            var titleColumn = new Column();
            titleColumnSet.Columns.Add(titleColumn);
            AddTextBlock(titleColumn, $"{model.location.city} の天気", TextSize.ExtraLarge, HorizontalAlignment.Center);

            // 本文作成 | Create body of card
            // 天気情報をセット | Set weather
            var mainColumnSet = new ColumnSet();
            card.Body.Add(mainColumnSet);

            foreach (var item in model.forecasts)
            {
                var mainColumn = new Column();
                mainColumnSet.Columns.Add(mainColumn);

                // 天気データの取得と加工 | Get & modify weather data
                string day = item.dateLabel;
                string date = DateTime.Parse(item.date).Date.ToString("M/d");

                // temperature が null の場合は "--" に変換 | Replace null data to "--" in temperature
                string maxTemp, minTemp;
                try
                {
                    maxTemp = item.temperature.max.celsius;
                    minTemp = item.temperature.min.celsius;
                }
                catch
                {
                    maxTemp = "--";
                    minTemp = "--";
                }

                // データのセット | Set data to card
                AddTextBlock(mainColumn, $"{day}({ date})", TextSize.Large, HorizontalAlignment.Center);
                AddTextBlock(mainColumn, $"{maxTemp} / {minTemp} °C", TextSize.Medium, HorizontalAlignment.Center);
                AddImage(mainColumn, item.image.url, ImageSize.Medium, HorizontalAlignment.Center);
            }

        }

        private static void AddWeather(WeatherModel model, AdaptiveCard card, string selectedDay)
        {
            // タイトル作成
            var titleColumnSet = new ColumnSet();
            card.Body.Add(titleColumnSet);

            var titleColumn = new Column();
            titleColumnSet.Columns.Add(titleColumn);
            AddTextBlock(titleColumn, $"{model.location.city} の天気", TextSize.ExtraLarge, HorizontalAlignment.Center);

            // 本文作成
            // 天気情報をセット
            var mainColumnSet = new ColumnSet();
            card.Body.Add(mainColumnSet);


            foreach (var forcast in model.forecasts)
            {
                // Todayが取得出来ている場合は、dateLabel = "今日" の場合のみセット (= else の操作を行う)
                // If get "Today" set data only where dateLabel = "今日" (= do "else" work)
                if (selectedDay == "Today" && forcast.dateLabel != "今日")
                {
                }
                else if (selectedDay == "Tomorrow" && forcast.dateLabel != "明日")
                {
                }
                else if (selectedDay == "DayAfterTomorrow" && forcast.dateLabel != "明後日")
                {
                }
                else
                {
                    var mainColumn = new Column();
                    mainColumnSet.Columns.Add(mainColumn);

                    // 天気データの取得と加工
                    string day = forcast.dateLabel;
                    string date = DateTime.Parse(forcast.date).Date.ToString("M/d");

                    // temperature が null の場合は "--" に変換
                    string maxTemp, minTemp;
                    try
                    {
                        maxTemp = forcast.temperature.max.celsius;
                        minTemp = forcast.temperature.min.celsius;
                    }
                    catch
                    {
                        maxTemp = "--";
                        minTemp = "--";
                    }

                    // データのセット
                    AddTextBlock(mainColumn, $"{day}({date})", TextSize.Large, HorizontalAlignment.Center);
                    AddTextBlock(mainColumn, $"{maxTemp} / {minTemp} °C", TextSize.Medium, HorizontalAlignment.Center);
                    AddImage(mainColumn, forcast.image.url, ImageSize.Medium, HorizontalAlignment.Center);

                }

            }

        }

        private static void AddCurrentWeather(WeatherModel model, AdaptiveCard card)
        {
            // タイトル作成 | Create title of card
            var titleColumnSet = new ColumnSet();
            card.Body.Add(titleColumnSet);

            var titleColumn = new Column();
            titleColumnSet.Columns.Add(titleColumn);

            var locationText = new TextBlock()
            {
                Text = $"{model.location.city} の天気",
                Size = TextSize.ExtraLarge,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            titleColumn.Items.Add(locationText);

            // 本文作成 | Create body of card
            // 天気情報をセット | Set weather
            var mainColumnSet = new ColumnSet();
            card.Body.Add(mainColumnSet);

            var mainColumn = new Column();
            mainColumnSet.Columns.Add(mainColumn);

            var mainText = new TextBlock()
            {
                Text = $"{model.publicTime.Date.ToString("M/d")}",
                Size = TextSize.Large,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainColumn.Items.Add(mainText);

            // 天気アイコンをセット | Set weather icon
            var mainImage = new AdaptiveCards.Image();
            mainImage.Url = model.forecasts[0].image.url;
            mainImage.HorizontalAlignment = HorizontalAlignment.Center;
            mainColumn.Items.Add(mainImage);
        }
    }
}