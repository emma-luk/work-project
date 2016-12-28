﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Fpm.MainUI.Helpers;
using Fpm.MainUI.Models;
using Fpm.ProfileData;
using Fpm.ProfileData.Entities.Profile;

namespace Fpm.MainUI.Controllers
{
    public class DocumentsController : Controller
    {
        public const int MaxFileSizeInBytes = 50000000/*50MB*/;

        private readonly ProfilesReader _reader = ReaderFactory.GetProfilesReader();
        private readonly ProfilesWriter _writer = ReaderFactory.GetProfilesWriter();

        public ActionResult Index()
        {
            var profileId = Request.Params["selectedProfile"];
            var model = new DocumentsGridModel
            {
                SortBy = "Sequence",
                SortAscending = true,
                CurrentPageIndex = 1,
                PageSize = 100
            };

            var user = UserDetails.CurrentUser();
            var userProfiles = user.GetProfilesUserHasPermissionsTo().ToList();

            AssignProfileList(model, userProfiles);
            AssignProfileId(model, profileId, userProfiles);

            GetDocumentItems(model);



          //  Session["GetAllProfile"] = model.ProfileList;

            //ViewBag.GetAllProfile = model;

            return View(model);
        }

        [HttpPost]
        public ActionResult Upload(string selectedProfileId)
        {
            if (Request != null)
            {
                int profileId = -1;
                if (!string.IsNullOrEmpty(selectedProfileId))
                {
                    profileId = Convert.ToInt32(selectedProfileId);
                }

                HttpPostedFileBase file = Request.Files["fileToBeUploaded"];
                if (file != null)
                {
                    byte[] uploadedFile = new byte[file.InputStream.Length];
                    file.InputStream.Read(uploadedFile, 0, uploadedFile.Length);

                    if (uploadedFile.Length > MaxFileSizeInBytes)
                    {
                        throw new FpmException("Max file upload size is 50 MB");
                    }

                    var fileName = Path.GetFileName(file.FileName);

                    // check is filename unique.
                    var docs = _reader.GetDocuments(fileName);
                    if (docs.Count > 0)
                    {
                        // now check is this name used with current profile.
                        var docFromDatabase = docs.FirstOrDefault(x => x.ProfileId == profileId);
                        if (docFromDatabase != null)
                        {
                            docFromDatabase.ProfileId = profileId;
                            docFromDatabase.FileName = fileName;
                            docFromDatabase.FileData = uploadedFile;
                            docFromDatabase.UploadedBy = new CurrentUser().Name;
                            docFromDatabase.UploadedOn = DateTime.Now;

                            _writer.UpdateDocument(docFromDatabase);
                        }
                    }
                    else
                    {
                        // else overwrite current file for selected profile.
                        var doc = new Document
                        {
                            ProfileId = profileId,
                            FileName = fileName,
                            FileData = uploadedFile,
                            UploadedBy = new CurrentUser().Name,
                            UploadedOn = DateTime.Now
                        };
                        _writer.NewDocument(doc);
                    }
                }
            }

            return RedirectToAction("Index", new { selectedProfile = selectedProfileId });
        }

        public ActionResult IsFileNameUnique(string filename, string selectedProfileId)
        {
            if (string.IsNullOrEmpty(selectedProfileId))
            {
                return new HttpStatusCodeResult(400, "bad request");
            }

            var profileId = Convert.ToInt32(selectedProfileId);
            var fileNameHelper = new FileNameHelper();

            return new JsonResult
            {
                Data = fileNameHelper.IsUnique(filename, profileId),
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        [HttpPost]
        [Route("documents/delete")]
        public ActionResult Delete(int id)
        {
            Document doc = _reader.GetDocument(id);

            if (doc == null)
            {
                throw new FpmException("Content item could not be deleted with id " + id);
            }

            if (UserDetails.CurrentUser().HasWritePermissionsToProfile(doc.ProfileId))
            {
                _writer.DeleteDocument(doc);

                return new JsonResult
                {
                    Data = "Success",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            throw new FpmException("User does not have the right to delete document " + id);
        }

        private static void AssignProfileId(DocumentsGridModel model, string profileId,
            IList<ProfileDetails> profilesThatUserCanEdit)
        {
            // If request params don't have any selected profile, use the first profile to populate the model
            if (profilesThatUserCanEdit.Count > 0)
            {
                model.ProfileId = profileId == null
                    ? profilesThatUserCanEdit[0].Id
                    : int.Parse(profileId);
            }
        }

        private void AssignProfileList(DocumentsGridModel model, IList<ProfileDetails> profilesDetails)
        {
            IEnumerable<SelectListItem> userProfilesForModel = profilesDetails.Select(c => new SelectListItem
            {
                Text = c.Name.ToString(CultureInfo.InvariantCulture),
                Value = c.Id.ToString(CultureInfo.InvariantCulture)
            });

            model.ProfileList = userProfilesForModel;
        }

        private void GetDocumentItems(DocumentsGridModel model)
        {
            model.DocumentItems = _reader.GetDocuments(model.ProfileId);
        }
    }
}