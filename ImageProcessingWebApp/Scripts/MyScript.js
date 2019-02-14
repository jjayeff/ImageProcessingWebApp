function ShowImagePreview(imageUploader) {
    var cshtml = {
        loaderMore_id: '#loaderMore',
        faceCanvas_img: 'faceCanvas_img',
        faceCanvas_img_id: '#faceCanvas_img',
        faceCanvas: 'faceCanvas',
        divRectangle_box: 'divRectangle_box',
        divRectangle_box_class: '.divRectangle_box',
        facePreview_thumb_small: 'facePreview_thumb_small_1',
        facePreview_thumb_small_id: '#facePreview_thumb_small_1'
    };
    DetectFace(imageUploader, cshtml);
}

function ShowImagePreview1(imageUploader) {
    var cshtml = {
        loaderMore_id: '#loaderMore1',
        faceCanvas_img: 'faceCanvas_img1',
        faceCanvas_img_id: '#faceCanvas_img1',
        faceCanvas: 'faceCanvas1',
        divRectangle_box: 'divRectangle_box1',
        divRectangle_box_class: '.divRectangle_box1',
        facePreview_thumb_small: 'facePreview_thumb_small_2',
        facePreview_thumb_small_id: '#facePreview_thumb_small_2'
    };
    DetectFace(imageUploader, cshtml, false);
}

function DetectFace(imageUploader, cshtml, check = true) {
    if (imageUploader.files && imageUploader.files[0]) {
        $('#loaderMoreupl').show();
        var formdata = new FormData(); //FormData object
        for (i = 0; i < imageUploader.files.length; i++) {
            //Appending each file to FormData object
            formdata.append(imageUploader.files[i].name, imageUploader.files[i]);
        }

        //File Save 
        var xhr = new XMLHttpRequest();
        xhr.open('POST', '/FaceDetection/SaveCandidateFiles');
        xhr.send(formdata);
        xhr.onreadystatechange = function () {
            if (xhr.readyState == 4 && xhr.status == 200) {
                //console.log(xhr.responseText)
                $('#loaderMoreupl').hide();
                $(cshtml.loaderMore_id).show();
                $.ajax({
                    type: "GET",
                    url: '/FaceDetection/GetDetectedFaces',
                    contentType: false,
                    processData: false,
                    success: function (result) {
                        $(cshtml.loaderMore_id).hide();
                        console.log(result);
                        //Reset element
                        $(cshtml.faceCanvas_img_id).remove();
                        $(cshtml.divRectangle_box_class).remove();
                        $(cshtml.facePreview_thumb_small_id).remove();
                        //get element byID
                        var canvas = document.getElementById(cshtml.faceCanvas);

                        //add image element
                        var elemImg = document.createElement("img");
                        elemImg.setAttribute("src", result.QueryFaceImage);
                        elemImg.setAttribute("width", result.MaxImageSize);
                        elemImg.id = cshtml.faceCanvas_img;
                        canvas.append(elemImg);
                        result.FaceRectangles.forEach(function (imgs, i) {

                            //Create rectangle for every face
                            var divRectangle = document.createElement('div');
                            var width = imgs.Width;
                            var height = imgs.Height;
                            var top = imgs.Top;
                            var left = imgs.Left;

                            //Style Div
                            divRectangle.className = cshtml.divRectangle_box;
                            divRectangle.style.width = width + 'px';
                            divRectangle.style.height = height + 'px';
                            divRectangle.style.position = 'absolute';
                            divRectangle.style.top = top + 'px';
                            divRectangle.style.left = left + 'px';
                            divRectangle.style.zIndex = '999';
                            divRectangle.style.border = '1px solid #4CFF33';
                            divRectangle.style.margin = '0';
                            divRectangle.id = 'divRectangle_' + (i + 1);

                            //Generate rectangles
                            canvas.append(divRectangle);

                        });

                        var wrapper = $('.facePreview_thumb_small'), container, list;
                        result.FaceInfo.forEach(function (item, i) {
                            container = $('<div class="col-sm-12" id="' + cshtml.facePreview_thumb_small + '"></div>');
                            wrapper.append(container);
                            container.append('<div class="col-sm-3"><img src="' + item.FilePath + '" width="100" /></div>');
                            list = $('<div class="col-sm-8"><ul></ul></div >');
                            container.append(list);
                            list.append('<li>Age: ' + item.Age + '</li>');
                            list.append('<li>Gender: ' + item.Gender + '</li>');
                            list.append('<li>' + item.IsSmiling + ', ' + item.Glasses + '</li>');
                            list.append('<li>' + item.Identify + ' (' + item.Confidence + ')</li>');
                        });

                        //OCR
                        if (check) {
                            document.getElementById("NameThai").value = result.OCRData.Data.NameThai;
                            document.getElementById("NameEng").value = result.OCRData.Data.NameEng;
                            document.getElementById("CitizenId").value = result.OCRData.Data.CitizenId;
                            document.getElementById("DOBThai").value = result.OCRData.Data.DOBThai;
                            document.getElementById("DOBEng").value = result.OCRData.Data.DOBEng;
                            document.getElementById("Religion").value = result.OCRData.Data.Religion;
                            document.getElementById("Address").value = result.OCRData.Data.Address;
                        }

                    },
                    error: function (xhr, status, p3, p4) {
                        var err = "Error " + " " + status + " " + p3 + " " + p4;
                        if (xhr.responseText && xhr.responseText[0] == "{")
                            err = JSON.parse(xhr.responseText).Message;
                        console.log(err);
                    }
                });
            }
        }

    }
}