using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OitavaAgenda.Options;
using System.Globalization;

namespace OitavaAgenda
{
    public class ReservationRequest
    {
        public required string EventDate { get; set; }
        public required string StartTime { get; set; }
        public required string EndTime { get; set; }
        public required string Space { get; set; }
        public required string EventTitle { get; set; }
        public required string EventOwner { get; set; }
        public required string ContactPhone { get; set; }
        public string? EventNotes { get; set; }
    }

    public class FunctionSchedule(
        IOptions<GoogleCalendarOitavaBetimOptions> googleCalendarOitavaBetimOptions,
        IOptions<GoogleCalendarOitavaBetimSpacesOptions> googleCalendarOitavaBetimSpacesOptions)
    {
        private readonly IOptions<GoogleCalendarOitavaBetimOptions> _googleCalendarOitavaBetimOptions = googleCalendarOitavaBetimOptions;
        private readonly IOptions<GoogleCalendarOitavaBetimSpacesOptions> _googleCalendarOitavaBetimSpacesOptions = googleCalendarOitavaBetimSpacesOptions;

        [Function("agendar")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ReservationRequest>(requestBody);
                if (data == null)
                {
                    return new BadRequestObjectResult(new
                    {
                        message = "N�o � poss�vel reconhecer os dados para realizar uma reserva."
                    });
                }

                // Autentica��o com o Google Calendar API.
                var credential = GoogleCredential.FromJsonParameters(_googleCalendarOitavaBetimOptions.Value)
                                    .CreateScoped(CalendarService.Scope.Calendar);
                var service = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "oitavaigreja"
                });

                // Espa�o selecionado (Calend�rio selecionado).
                if (!_googleCalendarOitavaBetimSpacesOptions.Value.TryGetValue(data.Space, out var calendarId))
                {
                    return new BadRequestObjectResult(new
                    {
                        message = "Espa�o inv�lido."
                    });
                }

                // Data de in�cio e fim do evento.
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); // Fuso hor�rio de Bras�lia
                var eventDateTimeStart = DateTime.Parse($"{data.EventDate}T{data.StartTime}", CultureInfo.InvariantCulture);
                    eventDateTimeStart = TimeZoneInfo.ConvertTimeToUtc(eventDateTimeStart, timeZone);
                var eventDateTimeEnd = DateTime.Parse($"{data.EventDate}T{data.EndTime}", CultureInfo.InvariantCulture);
                    eventDateTimeEnd = TimeZoneInfo.ConvertTimeToUtc(eventDateTimeEnd, timeZone);

                // Valida��o espa�o reservado para data e hora informada.
                var events = service.Events.List(calendarId);
                events.TimeMinDateTimeOffset = eventDateTimeStart;
                events.TimeMaxDateTimeOffset = eventDateTimeEnd;
                events.SingleEvents = true;
                var eventList = await events.ExecuteAsync();
                if (eventList.Items.Count > 0)
                {
                    return new BadRequestObjectResult(new
                    {
                        message = "Est� espa�o j� se econtra reservado para a data e hor�rio."
                    });
                }

                // Registra novo evento.
                var newEvent = new Event()
                {
                    Summary = data.EventTitle,
                    Location = data.Space,
                    Description = $"{data.EventNotes}\n\nRespons�vel: {data.EventOwner}\nContato: {data.ContactPhone}",
                    Start = new EventDateTime()
                    {
                        DateTimeDateTimeOffset = eventDateTimeStart,
                        TimeZone = "America/Sao_Paulo"
                    },
                    End = new EventDateTime()
                    {
                        DateTimeDateTimeOffset = eventDateTimeEnd,
                        TimeZone = "America/Sao_Paulo"
                    }
                };

                await service.Events.Insert(newEvent, calendarId).ExecuteAsync();
                return new OkObjectResult(new
                {
                    Message = "Programa��o criada com sucesso."
                });
            }
            catch(Exception ex)
            {
                return new BadRequestObjectResult(new
                {
                    message = ex.Message
                });
            }
        }
    }
}
