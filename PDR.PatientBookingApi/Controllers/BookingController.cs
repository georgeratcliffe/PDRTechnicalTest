using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PDR.PatientBooking.Data;
using PDR.PatientBooking.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PDR.PatientBookingApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingController : ControllerBase
    {
        private readonly PatientBookingContext _context;

        public BookingController(PatientBookingContext context)
        {
            _context = context;
        }

        [HttpGet("patient/{identificationNumber}/next")]
        public IActionResult GetPatientNextAppointment(long identificationNumber)
        {
            // Leave out Where(x => x.StartTime > DateTime.Now) filter to remove expired bookings
            var bookings = _context.Order.Where(x => x.PatientId == identificationNumber).OrderByDescending(x => x.StartTime).ToList();

            if (bookings.Count == 0)
                return StatusCode(400, new { message = "No bookings found" });

            if (HttpContext.Session.GetString("Starttime") != null)
            {
                DateTime startTime = DateTime.Parse(HttpContext.Session.GetString("Starttime"));

                if (bookings.Where(o => o.StartTime > startTime).Count() > 0)
                    bookings = bookings.Where(o => o.StartTime > startTime).OrderBy(x => x.StartTime).ToList();
            }

            var booking = bookings.FirstOrDefault();

            return Ok(new
            {
                booking.Id,
                booking.DoctorId,
                booking.StartTime,
                booking.EndTime
            });
        }

        [HttpPost()]
        public IActionResult AddBooking(NewBooking newBooking)
        {
            if (newBooking.StartTime < DateTime.Now)
                return StatusCode(400, new { message = "Cannot book past date" });

            var bookingId = new Guid();
            var bookingStartTime = newBooking.StartTime;
            var bookingEndTime = newBooking.EndTime;
            var bookingPatientId = newBooking.PatientId;
            var bookingPatient = _context.Patient.FirstOrDefault(x => x.Id == newBooking.PatientId);
            var bookingDoctorId = newBooking.DoctorId;
            var bookingDoctor = _context.Doctor.FirstOrDefault(x => x.Id == newBooking.DoctorId);
            var bookingSurgeryType = _context.Patient.FirstOrDefault(x => x.Id == bookingPatientId).Clinic.SurgeryType;

            if (bookingDoctor.Orders.Any(o =>
             (o.StartTime > bookingStartTime && o.StartTime < bookingEndTime)
              || (o.EndTime > bookingStartTime && o.EndTime < bookingEndTime)
              || (o.StartTime < bookingStartTime && o.EndTime > bookingEndTime)
              || (o.StartTime > bookingStartTime && o.EndTime < bookingEndTime)
              || (o.StartTime == bookingStartTime && o.EndTime == bookingEndTime)
            ))
                return StatusCode(400, new { message = "Doctor is busy" });

            var myBooking = new Order
            {
                Id = bookingId,
                StartTime = bookingStartTime,
                EndTime = bookingEndTime,
                PatientId = bookingPatientId,
                DoctorId = bookingDoctorId,
                Patient = bookingPatient,
                Doctor = bookingDoctor,
                SurgeryType = (int)bookingSurgeryType
            };

            _context.Order.Add(myBooking);
            _context.SaveChanges();

            return StatusCode(201);
        }


        [HttpDelete("patient/{identificationNumber}/{Id}")]
        public IActionResult DeletePatientAppointment(long identificationNumber, Guid Id)
        {
            var booking = _context.Order.Where(x => x.Patient.Id == identificationNumber && x.Id == Id).FirstOrDefault();

            if (booking == null)
                return StatusCode(400, new { message = "Booking does not exist" });

            HttpContext.Session.SetString("Starttime", booking.StartTime.ToString());

            _context.Order.Remove(booking);
            _context.SaveChanges();

            return StatusCode(200, new { message = "Booking Removed" });
        }


        public class NewBooking
        {
            public Guid Id { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public long PatientId { get; set; }
            public long DoctorId { get; set; }
        }

        // No update/PUT method requested so can ignore this code
        //private static MyOrderResult UpdateLatestBooking(List<Order> bookings2, int i)
        //{
        //    MyOrderResult latestBooking;
        //    latestBooking = new MyOrderResult();
        //    latestBooking.Id = bookings2[i].Id;
        //    latestBooking.DoctorId = bookings2[i].DoctorId;
        //    latestBooking.StartTime = bookings2[i].StartTime;
        //    latestBooking.EndTime = bookings2[i].EndTime;
        //    latestBooking.PatientId = bookings2[i].PatientId;
        //    latestBooking.SurgeryType = (int)bookings2[i].GetSurgeryType();

        //    return latestBooking;
        //}

        private class MyOrderResult
        {
            public Guid Id { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public long PatientId { get; set; }
            public long DoctorId { get; set; }
            public int SurgeryType { get; set; }
        }
    }
}