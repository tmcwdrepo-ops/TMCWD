// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

HTMLElement.prototype.SearchableDropdown = async function (options) {
    const elSelect = this;
    //var isListenersAttached = false;

    if (elSelect.tagName.toLowerCase() === "select") {
        
        const parentDiv = elSelect.parentElement;

        if (parentDiv) {

            var elOptions =[];
            const inputTexts = parentDiv.getElementsByClassName("dropdown-input");
            const hiddenTexts = parentDiv.getElementsByClassName("selected-value");
            const elContent = parentDiv.getElementById("dropdown-options");
            const btnCollapse = parentDiv.querySelector("button");
            const icon = btnCollapse.querySelector("i");

            if (!inputTexts || !inputTexts[0]) return;
            if (!hiddenTexts || !hiddenTexts[0]) return;
            if (!elContent || !elContent[0]) return;
            if (!btnCollapse) return;
            if (!icon) return;


            var itemClient = new WebClient(options.getItemUrl, options.getItemParameter);
            var itemData = await itemClient.getAsync();

            if (itemData) {
                [...itemData].forEach((item) => {
                    const lnk = document.createElement('a');
                    lnk.href = '#';
                    const par = document.createElement('p');
                    par.classList.add('w-full', 'px-2', 'py-2', 'hover:bg-blue-500', 'hover:text-white');
                    par.textContent = item.name;
                    lnk.appendChild(par);
                    elContent[0].appendChild(lnk);
                    elOptions.push(lnk);

                    lnk.addEventListener('click', (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();

                        inputTexts[0].value = item[options.dataMemberText];
                        hiddenTexts[0].value = item[options.dataMemberValue];

                        icon.classList.toggle('fa-chevron-down');
                        icon.classList.toggle('fa-chevron-up');f
                        elContent[0].classList.toggle("hidden");

                        if (options.onSelectChange) {
                            options.onSelectChange(data);
                        }
                    });

                });
            }

            btnCollapse.addEventListener("click", (evt) => {
                evt.preventDefault();
                //elContent = parentDiv.getElementsByClassName("dropdown-options");
                icon.classList.toggle('fa-chevron-down');
                icon.classList.toggle('fa-chevron-up');
                elContent[0].classList.toggle("hidden");

                //const inputs = parentDiv.getElementsByClassName("dropdown-input");

                //if (isListenersAttached) return;

                // var contents = elContent[0].children;

                // [...contents].forEach((element) => {
                //     elOptions.push(element);
                // });

                // var links = elContent[0].querySelectorAll('a');
                // if (links) {

                //     [...links].forEach((link) => {
                //         link.addEventListener("click", (evt) => {
                //             evt.preventDefault();
                //             evt.stopPropagation();
                                
                //             const elSelectedValues = parentDiv.getElementsByClassName("selected-value");
                //             var itemId = evt.target.parentElement.dataset.itemid;
                //             var itemName = evt.target.parentElement.dataset.itemname;
                //             var unitSellingPrice = evt.target.parentElement.dataset.unitsellingprice;
                //             if (inputs) inputs[0].value = itemName;
                //             if (elSelectedValues) elSelectedValues[0].value = itemId;
                //             if (selectCallBack) selectCallBack(itemId, itemName, unitSellingPrice);
                //             icon.classList.toggle('fa-chevron-down');
                //             icon.classList.toggle('fa-chevron-up');
                //             elContent[0].classList.toggle("hidden");
                //         });
                //     });
                // }

            });

            inputTexts[0].addEventListener("input", (evt) => {
                evt.preventDefault();
                evt.stopPropagation();
                var searchString = evt.target.value.toLowerCase();
                if (searchString == '') {
                    hiddenTexts[0].value = '';
                    if (options.onSelectionClear) options.onSelectionClear();
                }

                const elFiltered = [...elOptions].filter((element, index, array) => {
                    var parContent = element.querySelector("p");
                    return parContent.textContent.toLowerCase().includes(searchString.toLowerCase());
                });

                elContent[0].replaceChildren();

                var result = searchString.trim() == '' ? elOptions : elFiltered;
                [...result].forEach((el) => {
                    elContent[0].appendChild(el);
                });
                if (elContent[0].classList.contains('hidden')) {
                    icon.classList.toggle('fa-chevron-down');
                    icon.classList.toggle('fa-chevron-up');
                    elContent[0].classList.toggle("hidden");
                }
            });

            //isListenersAttached = true;

        }
    }
};

HTMLElement.prototype.clearDropdown = function () {
    var select = this;

    if (select.tagName !== 'SELECT') return;

    var parentContainer = select.parentElement;

    var inputText = parentContainer.querySelector('input[type="text"]');
    var inputHidden = parentContainer.querySelector('input[type="hidden"]');
    if (inputText) inputText.value = '';
    if (inputHidden) inputHidden.value = '';
}

HTMLElement.prototype.DataTable = async function(options){
    const table = this;
    if (table.tagName !== "TABLE" || options.columns == null) return;

    var columnCount = options.columns.length;

    if (options.buttons != null && options.buttons.length > 0) columnCount++;

    const tdNoRecord = document.createElement("td");
    tdNoRecord.scope = "col";
    tdNoRecord.colSpan = columnCount;
    tdNoRecord.classList.add('px-6', 'py-3', 'text-center');
    tdNoRecord.textContent = options.noRecordText || "No record(s) found . . .";

    const trDefault = document.createElement("tr");

    var tbody = table.querySelector("tbody");
    if (tbody == null) {
        tbody = document.createElement("tbody");
        table.appendChild(tbody);
    }

    if (options.getUrl == '') {
        trDefault.appendChild(tdNoRecord);
        tbody.appendChild(trDefault);
        return;
    }

    if (options.processing) {
        tbody.replaceChildren();
        trDefault.innerHTML = `
        <td colspan="${columnCount}" class="w-full h-32 text-center align-middle">
            <div class="place-items-center">
               <svg class="w-12 h-12 text-gray-300 animate-spin" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg" width="24" height="24">
                  <path
                    d="M32 3C35.8083 3 39.5794 3.75011 43.0978 5.20749C46.6163 6.66488 49.8132 8.80101 52.5061 11.4939C55.199 14.1868 57.3351 17.3837 58.7925 20.9022C60.2499 24.4206 61 28.1917 61 32C61 35.8083 60.2499 39.5794 58.7925 43.0978C57.3351 46.6163 55.199 49.8132 52.5061 52.5061C49.8132 55.199 46.6163 57.3351 43.0978 58.7925C39.5794 60.2499 35.8083 61 32 61C28.1917 61 24.4206 60.2499 20.9022 58.7925C17.3837 57.3351 14.1868 55.199 11.4939 52.5061C8.801 49.8132 6.66487 46.6163 5.20749 43.0978C3.7501 39.5794 3 35.8083 3 32C3 28.1917 3.75011 24.4206 5.2075 20.9022C6.66489 17.3837 8.80101 14.1868 11.4939 11.4939C14.1868 8.80099 17.3838 6.66487 20.9022 5.20749C24.4206 3.7501 28.1917 3 32 3L32 3Z"
                    stroke="currentColor" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"></path>
                  <path
                    d="M32 3C36.5778 3 41.0906 4.08374 45.1692 6.16256C49.2477 8.24138 52.7762 11.2562 55.466 14.9605C58.1558 18.6647 59.9304 22.9531 60.6448 27.4748C61.3591 31.9965 60.9928 36.6232 59.5759 40.9762"
                    stroke="currentColor" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" class="text-gray-900">
                  </path>
               </svg>
            </div>
        </td>`;
        tbody.appendChild(trDefault);
    }
    else {
        trDefault.appendChild(tdNoRecord);
        tbody.appendChild(trDefault);
    }
    
    var client = new WebClient(options.getUrl, null);
    var response = await client.getAsync();

    if (options.loadedCallback) options.loadedCallback(response);

    if (response == null || response.length == 0) {
        trDefault.replaceChildren();
        trDefault.appendChild(tdNoRecord);
        return;
    }

    var fragment = document.createDocumentFragment();

    response.forEach((item) => {
        const tr = document.createElement("tr");
        tr.className = options.rowClassNames ? options.rowClassNames : "bg-white border-b hover:bg-gray-50";
        if (options.stockCheck) {
            if (options.stockCheck.srcDataMember.trim() != '' && options.stockCheck.compareToDataMember.trim() != '') {
                var srcValue = item[options.stockCheck.srcDataMember.trim()];
                var compareValue = item[options.stockCheck.compareToDataMember.trim()];
                if (compareValue > srcValue) {
                    tr.classList.toggle('bg-red-300');
                }
            }
            else {
                if (tr.classList.contains('bg-red-300')) {
                    tr.classList.toggle('bg-red-300');
                }
            }
        }

        options.columns.forEach((column) => {
            const itemValue = item[column.dataMember];
            const td = document.createElement("td");
            //td.classList.add('px-6', 'py-3');
            td.className = column.className ?? 'px-6 py-3';
            if (column.isCentered) td.classList.add('text-center');
            if (column.isEditable) {
                td.innerHTML = `<p class='editable-content-text w-full'>${itemValue}</p>
                    <p class='editable-content-input w-full hidden'>
                        <input type="text" data-member="${column.dataMember}" class='w-full border rounded-md p-1 text-md input-col-${column.dataMember}' value="${itemValue}" />
                    </p>`;
            }
            else {
                if (column.displays && column.displays.length > 0) {
                    const displays = column.displays.filter((item, index, array) => {
                        return (item.value + '').toLowerCase() === (itemValue + '').toLowerCase();
                    });

                    if (displays && displays.length > 0) {
                        const toDisplay = displays[0];
                        const icon = document.createElement("i");
                        icon.className = toDisplay.icon;
                        td.appendChild(icon);
                    }
                    else {
                        td.textContent = itemValue;
                    }
                }
                else td.textContent = itemValue;
            }
            tr.appendChild(td);
        });

        if (options.buttons != null && options.buttons.length > 0) {
            const buttonTd = document.createElement("td");
            buttonTd.className = options.buttonColumnClassNames ? options.buttonColumnClassNames : "w-64";
            options.buttons.forEach((button) => {
                const elButton = document.createElement("button");
                elButton.dataset.isset = false;
                elButton.className = "text-center hover:rounded p-2.5 hover:bg-blue-900 hover:text-white mr-2";
                const icon = document.createElement("i");
                icon.className = button.className;
                elButton.appendChild(icon);
                buttonTd.appendChild(elButton);

                if (button.isEditButton) {
                    elButton.addEventListener("click", (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();

                        if (button.location && button.location.trim().length > 0) {
                            window.location.href = button.location;
                            return;
                        }

                        const contents = tr.querySelectorAll('p');

                        [...contents].forEach((paragraph) => {
                            const input = paragraph.querySelector('input');
                            if (input) {
                                paragraph.classList.toggle('hidden');
                                var member = input.dataset.member;
                                input.value = item[member];
                            }
                            else paragraph.classList.toggle('hidden');
                        });
                        const icon = evt.target;
                        if (icon) {
                            button.toggleIcons.forEach((toggleIcon) => {
                                icon.classList.toggle(toggleIcon);
                            });
                        }
                        evt.target.parentElement.dataset.isset = true;
                    });
                }
                else if (button.isSaveButton) {
                    elButton.addEventListener("click", (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();

                        const inputs = tr.querySelectorAll('input');
                        var newItem = item;

                        if (inputs) {
                            options.columns.forEach((col) => {
                                const inps = [...inputs].filter((element, index, array) => {
                                    return element.classList.contains('input-col-' + col.dataMember);
                                });

                                if (inps == null || inps.length == 0) return;

                                const inp = inps[0];

                                if (inp) {
                                    newItem[col.dataMember] = inp.value;
                                }
                            });

                            if (button.clickCallback) {
                                button.clickCallback(newItem);
                            }
                        }

                    });
                }
                else {
                    elButton.addEventListener("click", (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();
                        if (button.clickCallback) button.clickCallback(item);
                        evt.target.parentElement.dataset.isset = true;
                    });
                }

            });
            tr.appendChild(buttonTd);
        }

        fragment.appendChild(tr);

    });

    tbody.replaceChildren();
    tbody.appendChild(fragment);

};

HTMLElement.prototype.Collapsible = function () {
    const div = this;
    if (div.tagName !== "DIV") return;
    if (div.dataset.accordion == null || div.dataset.accordion.toLowerCase() != 'collapse') return;
    //write code here
    const buttons = div.getElementsByClassName('collapsible-button');
    if (buttons == null || buttons.length <= 0) return;

    [...buttons].forEach((button) => {
        button.addEventListener('click', (evt) => {
            evt.preventDefault();
            evt.stopPropagation();
            const h2 = evt.target.closest('h2');
            if (h2 == null) return;
            const bodyDiv = h2.nextElementSibling;
            if (bodyDiv == null) return;
            const icon = evt.target.querySelector('svg');
            if (icon == null) return;
            bodyDiv.classList.toggle('hidden');
            icon.classList.toggle('rotate-180');
        });
    });
};

HTMLElement.prototype.SetServices = async function (options) {
    const div = this;
    if (div.tagName !== "DIV") return;
    if (div.dataset.services == null || div.dataset.services != 'details') return;

    if (options.getServicesUrl == null || options.getServicesUrl.trim() == '') throw new Error('getServiceUrl option is not set');

    var typeClient = new WebClient(options.getServicesUrl, null);
    var types = await typeClient.getAsync();

    types.forEach((type) => {
        let serviceDiv = document.createElement("div");
        serviceDiv.className = "group flex items-center ps-4 pr-4 bg-neutral-primary-soft border border-default rounded-lg shadow-xs hover:bg-blue-900 hover:text-white hover:cursor-pointer";

        let checkbox = document.createElement("input");
        checkbox.className = "w-4 h-4 border border-default-medium rounded-xs bg-neutral-secondary-medium focus:ring-2 focus:ring-brand-soft hover:cursor-pointer";
        checkbox.id = "border-checkbox-" + type.id;
        checkbox.dataset.id = type.id;
        checkbox.dataset.withdetail = type.withDetail;
        checkbox.dataset.requiredaccount = type.isRequiredAccount;
        checkbox.value = '';
        checkbox.type = "checkbox";
        checkbox.checked = type.isSelected;
        checkbox.disabled = options.isDisabled;

        serviceDiv.appendChild(checkbox);

        checkbox.addEventListener("change", (evt) => {
            evt.stopPropagation();
            let checkboxes = div.querySelectorAll('input[type="checkbox"]');
            if (checkboxes) {
                var checkedBoxes = [...checkboxes].filter((element, index, array) => {
                    return element.checked;
                });

                var ids = [];
                [...checkedBoxes].forEach((box) => {
                    ids.push(box.dataset.id);
                });

                if (ids.length > 0) {
                    if (options.selectChange == null) return;
                    options.selectChange(ids);
                }
            }
        });

        let label = document.createElement("label");
        label.for = "border-checkbox-" + type.id;
        label.className = "select-none w-full py-4 ms-2 text-sm font-medium text-heading hover:cursor-pointer";
        label.textContent = type.name;
        serviceDiv.appendChild(label);

        if (type.withDetail) {
            let input = document.createElement("input");
            input.id = "txtDetail-" + type.id;
            input.type = "text";
            input.setAttribute("placeholder", "Detail here...");
            input.className = "bg-transparent border-none focus:ring-0 text-gray-700 w-full outline-none group-hover:text-white";
            serviceDiv.appendChild(input);
        }

        div.appendChild(serviceDiv);
    });

    //const checkboxes = div.querySelectorAll("input[type='checkbox']");

    //if (checkboxes == null || checkboxes.length <= 0) return;

    // [...checkBoxes].forEach((checkbox) => {
    //     checkbox.addEventListener("change", (evt) => {
    //         evt.preventDefault();
    //         var targetId = evt.target.id;
    //         var typeId = evt.target.dataset.id;
    //         var withDetail = evt.target.dataset.withdetail;
    //         var requiredAccount = evt.target.dataset.requiredaccount;
    //         if (requiredAccount == 'False') {
    //             [...checkBoxes].forEach((checkbox) => {
    //                 if (checkbox.id != targetId) {
    //                     checkbox.checked = false;
    //                 }
    //             });
    //         }
    //         else {
    //             var checkBoxesNoAccount = document.querySelectorAll('[data-requiredaccount="False"]');
    //             [...checkBoxesNoAccount].forEach((checkbox) => {
    //                 checkbox.checked = false;
    //             });
    //         }
    //     });
    // });
};

HTMLElement.prototype.Dropzone = function (options) {
    const dropzone = this;
    var filesForUpload = [];
    if (dropzone.tagName.toLowerCase() !== "div") return;

    if (!dropzone.classList.contains('drop-zone')) return;

    const fileUpload = dropzone.querySelector('input[type="file"]');

    dropzone.addEventListener("dragover", (evt) => {
        evt.preventDefault();
    });

    dropzone.addEventListener("drop", (evt) => {
        evt.preventDefault();
        if (handleFiles(evt.dataTransfer.files)) {
            if (!checkIsSizeAllowed(filesForUpload)) {
                if (options.onError) options.onError('File size exceeds maximum allowed size');
            }
        }
        if (options.onChange) options.onChange(filesForUpload);
    });

    if (fileUpload) {
        fileUpload.addEventListener("change", (evt) => {
            evt.preventDefault();
            if (handleFiles(evt.target.files)) {
                if (!checkIsSizeAllowed(filesForUpload)) {
                    if (options.onError) options.onError('The uploaded files exceeds the allowed maximum file size');
                }
            }
            if (options.onChange) options.onChange(filesForUpload);
        });
    }

    let handleFiles = function (files) {
        try {
            [...files].forEach((file) => {
                const pFile = document.createElement('p');
                const spanFilename = document.createElement('span');
                const imageIcon = document.createElement('i');
                const removeIcon = document.createElement('i');
                const lnkRemove = document.createElement('a');

                let isExists = checkFileExists(file);
                let isAllowed = checkIsFileAllowed(file);

                //if (!isAllowed) throw new Error("The file type of " + file.name + " is not allowed.")

                if (!isExists && isAllowed) {
                    filesForUpload.push(file);

                    const fileList = document.getElementById(options.fileListContainer);
                    const sizeInMB = convertBtoMB(file.size);

                    if (fileList) {
                        spanFilename.textContent = file.name + ' - ' + sizeInMB + 'MB' + ' - ' + file.type;
                        spanFilename.classList.add('mr-1');
                        imageIcon.classList.add('fa-solid', 'fa-file-image', 'mr-1');
                        removeIcon.classList.add('fa-solid', 'fa-xmark', 'text-red-500');
                        pFile.classList.add('w-full', 'flex', 'items-center');
                        pFile.appendChild(imageIcon);
                        pFile.appendChild(spanFilename);
                        lnkRemove.appendChild(removeIcon);
                        lnkRemove.href = '#';
                        pFile.appendChild(lnkRemove);
                        fileList.appendChild(pFile);
                    }

                    lnkRemove.addEventListener('click', (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();
                        pFile.remove();
                        if (options.onRemove) options.onRemove(file);

                    });

                }
                else if (!isAllowed) {
                    if (options.onError) options.onError('The file ' + file.name + ' is not allowed.');
                }
            });

        }
        catch (ex) {
            if (options.onError) options.onError(ex.message + ' - Exception');
            return false;
        }

        return true;
    };

    let checkFileExists = function (file) {
        let exists = filesForUpload.filter((item, index, array) => {
            return item.name === file.name && item.type === file.type;
        });

        return exists != null && exists.length > 0;
    };

    let checkIsSizeAllowed = function (files) {
        var sum = 0;
        [...files].forEach((file) => sum + convertBtoMB(file.size));
        return sum.toFixed(2) < (options.maxSizeInMB ?? 30);
    };

    let checkIsFileAllowed = function (file) {

        if (options.allowedTypes == null) return true;

        const isAllowed = options.allowedTypes.filter((type) => {
            return type == file.type
        });

        return isAllowed != null && isAllowed.length > 0;
    };

};

HTMLElement.prototype.LoadFindings = async function (options) {
    var findingsContainer = this;
    if (findingsContainer.tagName !== 'DIV') return;

    if (!findingsContainer.classList.contains('findings-container')) return;

    if (options.getImageDataUrl) {
        const loadImageClient = new WebClient(options.getImageDataUrl, null);
        var resp = await loadImageClient.getAsync();
        if (resp) {
            if (resp.findings) {
                resp.findings.forEach((finding) => {
                    const numberOfColumns = options.numberOfColumns ?? 4;
                    findingsContainer.replaceChildren();
                    const divFinding = document.createElement('div');
                    divFinding.classList.add('col-span-' + numberOfColumns, 'w-full');
                    const pFindingNarrative = document.createElement('p');
                    pFindingNarrative.classList.add('w-full', 'p-4', 'border', 'rounded-md', 'text-sm');
                    pFindingNarrative.textContent = finding.detail;
                    divFinding.appendChild(pFindingNarrative);
                    findingsContainer.appendChild(divFinding);
                });
            }

            if (resp.files) {
                resp.files.forEach((file) => {
                    const divImageItem = document.createElement('div');
                    divImageItem.classList.add('w-32', 'border', 'p-2', 'rounded-md', 'border-gray-400');
                    const img = document.createElement('img');
                    img.classList.add('w-full', 'h-full');
                    img.src = file.path + '/' + file.physicalFilename;

                    const lnk = document.createElement('a');
                    lnk.href = '#';
                    lnk.title = file.originalFilename;
                    lnk.alt = file.originalFilename;
                    lnk.appendChild(img);

                    divImageItem.appendChild(lnk);
                    findingsContainer.appendChild(divImageItem);

                    lnk.addEventListener('click', (evt) => {
                        evt.preventDefault();
                        evt.stopPropagation();
                        const imgModal = document.getElementById("image-viewer");
                        const img = imgModal.querySelector('img');
                        img.src = file.path + '/' + file.physicalFilename;
                        img.alt = file.originalFilename;
                        imgModal.classList.toggle('hidden');
                    });
                });
            }
            if (options.loadComplete) {
                options.loadComplete(true, "Successfully loaded findigs");
            }
        }
        else {
            if (options.loadComplete) {
                options.loadComplete(false, "No records found");
            }
        }
    }
};

HTMLElement.prototype.LoadImages = async function (options) {
    var imageContainer = this;
    if (imageContainer.tagName !== 'DIV') return;

    if (!imageContainer.classList.contains('image-container')) return;
    
    if (options.getImageDataUrl) {
        //const loadImageClient = new WebClient(options.getImageDataUrl, null);
        //var resp = await loadImageClient.getAsync();
        //if (resp) {
            const divImageItem = document.createElement('div');
            divImageItem.classList.add('w-32', 'border', 'p-2', 'rounded-md', 'border-gray-500');
            const img = document.createElement('img');
            img.classList.add('h-16', 'mb-2');
            img.src = '/images/25d4be42ce174de8b31791b886fe3dcf.png';
            const pName = document.createElement('p');
            pName.classList.add('w-full', 'text-center', 'text-xs', 'font-bold');
            pName.textContent = 'ict policy 2025.png';
            const lnk = document.createElement('a');
            lnk.href = '#';
            lnk.appendChild(img);
            lnk.appendChild(pName);
            divImageItem.appendChild(lnk);
            imageContainer.replaceChildren();
            imageContainer.appendChild(divImageItem);
        //}
    }
};

HTMLElement.prototype.ImageViewer = function () {
    const viewerContainer = this;
    var isInitialization = true;
    if (viewerContainer.tagName !== 'DIV') return;
    if (!viewerContainer.classList.contains('image-viewer')) return;

    if (!viewerContainer.classList.contains('absolute')) viewerContainer.classList.add('absolute');
    if (!viewerContainer.classList.contains('top-0')) viewerContainer.classList.add('top-0');
    if (!viewerContainer.classList.contains('left-0')) viewerContainer.classList.add('left-0');
    if (!viewerContainer.classList.contains('h-screen')) viewerContainer.classList.add('h-screen');
    if (!viewerContainer.classList.contains('w-screen')) viewerContainer.classList.add('w-screen');
    if (!viewerContainer.classList.contains('bg-gray-500')) viewerContainer.classList.add('bg-gray-500');
    if (!viewerContainer.classList.contains('bg-opacity-30')) viewerContainer.classList.add('bg-opacity-30');
    if (!viewerContainer.classList.contains('hidden')) viewerContainer.classList.add('hidden');

    const backDrop = document.createElement('div');
    backDrop.classList.add('absolute', 'inset-0');

    viewerContainer.appendChild(backDrop);

    const viewerPanel = document.createElement('div');
    viewerPanel.classList.add('relative', 'border', 'border-gray-500', 'z-10', 'top-1/2', 'bg-white', 'left-1/2', '-translate-y-1/2', '-translate-x-1/2');
    viewerPanel.classList.add('rounded-lg', 'text-black', 'w-1/2', 'h-5/6', 'transition-all', 'transition-discrete', 'delay-150', 'duration-300');

    viewerContainer.appendChild(viewerPanel);

    const controlPanel = document.createElement('div');
    controlPanel.classList.add('grid', 'grid-cols-2', 'w-full', 'pt-1', 'px-2.5', 'text-lg', 'gap-6', 'mb-1');

    const zoomButtonPanel = document.createElement('div');
    zoomButtonPanel.classList.add('text-left');

    const zoomInButton = document.createElement('a');
    zoomInButton.classList.add('image-viewer-zoom-in', 'focus:border-none', 'mr-2', 'hidden');
    zoomInButton.href = '#';

    var scales = ['scale-50', 'scale-75', 'scale-100'];

    zoomInButton.addEventListener('click', (evt) => {
        evt.preventDefault();
        evt.stopPropagation();

        for (let i = 0; i < scales.length; i++) {
            let scale = scales[i];
            if (viewerPanel.classList.contains(scale)) {
                let currentScaleClass = '';
                if (i + 1 >= scales.length) currentScaleClass = scales[scales.length - 1];
                else currentScaleClass = scales[i + 1];

                viewerPanel.classList.toggle(scale);
                viewerPanel.classList.toggle(currentScaleClass);
                break;
            }
        }

    });

    const zoomInIcon = document.createElement('i');
    zoomInIcon.classList.add('fa-solid', 'fa-circle-plus');

    zoomInButton.appendChild(zoomInIcon);

    zoomButtonPanel.appendChild(zoomInButton);

    const zoomOutButton = document.createElement('a');
    zoomOutButton.classList.add('image-viewer-close', 'focus:border-none', 'hidden');
    zoomOutButton.href = '#';

    zoomOutButton.addEventListener('click', (evt) => {
        evt.preventDefault();
        evt.stopPropagation();

        for (let i = 0; i < scales.length; i++) {
            let scale = scales[i];
            if (viewerPanel.classList.contains(scale)) {
                let currentScaleClass = '';
                if (i - 1 <= 0) currentScaleClass = scales[0];
                else currentScaleClass = scales[i - 1];

                viewerPanel.classList.toggle(scale);
                viewerPanel.classList.toggle(currentScaleClass);
                break;
            }
        }

    });

    const zoomOutButtonIcon = document.createElement('i');
    zoomOutButtonIcon.classList.add('fa-solid', 'fa-circle-minus');

    zoomOutButton.appendChild(zoomOutButtonIcon);

    zoomButtonPanel.appendChild(zoomOutButton);

    controlPanel.appendChild(zoomButtonPanel);

    const closeButtonPanel = document.createElement('div');
    closeButtonPanel.classList.add('text-right');

    const closeButton = document.createElement('a');
    closeButton.classList.add('image-viewer-close', 'focus:display-none');
    closeButton.href = '#';

    const closeIcon = document.createElement('i');
    closeIcon.classList.add('fa-solid', 'fa-xmark');

    closeButton.appendChild(closeIcon);
    closeButtonPanel.appendChild(closeButton);

    closeButton.addEventListener('click', (evt) => {
        evt.preventDefault();
        evt.stopPropagation();

        viewerContainer.classList.toggle('hidden');

    });

    controlPanel.appendChild(closeButtonPanel);

    viewerPanel.appendChild(controlPanel);

    const imageContainer = document.createElement('div');
    imageContainer.classList.add('w-full', 'h-full');
    const image = document.createElement('img');
    image.classList.add('pb-6', 'w-full', 'h-full');
    imageContainer.appendChild(image);

    viewerPanel.appendChild(imageContainer);
    viewerContainer.appendChild(viewerPanel);

};

const convertBtoMB = function (sizeInByte) {
    return (sizeInByte / (1024 * 1024)).toFixed(2);
};

class WebClient {

    url = "";
    data = null;
    constructor(url, data) {
        this.url = url;
        this.data = data;
    }

    async postAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(this.data)
        }).then(response => {
            if (!response.ok || response.status == 204)
                return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });
        
        return returnResult;
    }

    async postFileAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'POST',
            body: this.data
        }).then(response => {
            if (!response.ok || response.status == 204)
                return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });

        return returnResult;
    }

    async patchAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'PATCH',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(this.data)
        }).then(response => {
            if (!response.ok || response.status == 204) return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });

        return returnResult;
    }

    async getAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        }).then(response => {
            if (!response.ok || response.status == 204)
                return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });

        return returnResult;
    }

    async putAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(this.data)
        }).then(response => {
            if (!response.ok)
                return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });

        return returnResult;
    }

    async deleteAsync() {
        let returnResult = null;
        await fetch(this.url, {
            method: 'DELETE',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(this.data)
        }).then(response => {
            if (!response.ok)
                return null;
            return response.json();
        }).then(result => {
            returnResult = result;
        });

        return returnResult;
    }
};

class ModalDialog {

    dialog = null;
    cancelButton = null;
    submitButton = null;
    inputText = null;
    fnSubmitCallback = null;
    title = "";
    content = "";
    textTitle = "";

    constructor(params) {
        this.title = params.title;
        this.content = params.content;
        this.textTitle = params.textTile;
        this.fnSubmitCallback = params.submitCallback;
        this.#initialize(params.type);
    }

    show() {
        this.dialog.showModal();

        this.#cancelAddListener();
        this.#submitAddListener();
    }

    close() {
        this.dialog.close();
    }

    #initialize(type) {
        this.#setTitleAndContent(type);
        this.#setButton(type);
    }

    #setTitleAndContent(type) {
        var elTitle = null;
        var elContent = null;
        var elTextTitle = null;
        switch (type) {
            case 'deactivate':
                elTitle = document.getElementsByClassName("deactivate-title");
                elContent = document.getElementsByClassName("deactivate-content");
                break;
            case 'success':
                elTitle = document.getElementsByClassName("success-title");
                elContent = document.getElementsByClassName("success-content");
                break;
            case 'number':
                elTitle = document.getElementsByClassName("number-title");
                elContent = document.getElementsByClassName("number-content");
                elTextTitle = document.getElementsByClassName("number-text-title");
                break;
            case "text":
                elTitle = document.getElementsByClassName("text-title");
                elContent = document.getElementsByClassName("text-content");
                elTextTitle = document.getElementsByClassName("text-text-title");
                break;
            default:
                break;
        }

        if (elTitle) elTitle[0].textContent = this.title;
        if (elContent) elContent[0].textContent = this.content;
        if (elTextTitle) elTextTitle[0].textContent = this.textTitle;
    }

    #setButton(type) {
        var dialogClassName = ""
        var cancelButtonClassName = "";
        var submitButtonClassName = "";
        var inputClassName = ""
        switch (type) {
            case 'deactivate':
                dialogClassName = "deactivate-modal";
                cancelButtonClassName = "deactivate-cancel"
                submitButtonClassName = "deactivate-submit";
                break;
            case 'success':
                dialogClassName = "success-modal";
                cancelButtonClassName = "success-cancel"
                submitButtonClassName = "success-submit";
                break;
            case 'number':
                dialogClassName = "number-modal";
                cancelButtonClassName = "number-cancel"
                submitButtonClassName = "number-submit";
                inputClassName = "number-text";
                break;
            case 'text':
                dialogClassName = "text-modal";
                cancelButtonClassName = "text-cancel";
                submitButtonClassName = "text-submit";
                inputClassName = "text-text";
                break;
            default:
                dialogClassName = "";
                break;
        }
        var elDialog = document.getElementsByClassName(dialogClassName);
        var elCancel = document.getElementsByClassName(cancelButtonClassName);
        var elSubmit = document.getElementsByClassName(submitButtonClassName);
        var elText = document.getElementsByClassName(inputClassName);
        if (elDialog) this.dialog = elDialog[0];
        if (elCancel) this.cancelButton = elCancel[0];
        if (elSubmit) this.submitButton = elSubmit[0];
        if (elText) this.inputText = elText[0];
    }

    #cancelAddListener() {
        if (this.cancelButton == null) return;
        this.cancelButton.addEventListener("click", (evt) => {
            evt.preventDefault();
            this.dialog.close();
        }, { once: true });
    }

    #submitAddListener() {
        if (this.submitButton == null) return;
        this.submitButton.addEventListener("click", (evt) => {
            evt.preventDefault();
            if (this.fnSubmitCallback) {
                if (this.numberText == null) {
                    this.fnSubmitCallback();
                }
                else {
                    var textValue = this.numberText.value;
                    this.fnSubmitCallback(textValue);
                    this.dialog.close();
                }
            }
        }, { once: true });
    }

};

class Alert {
    constructor() { }

    show(options) {
        if (options) {
            const alertWindow = document.getElementById(options.type + 'Alert');
            var alertSpan = document.querySelector('#' + options.type + 'Alert span');
            if (options.content) {
                alertSpan.innerText = options.content;
            }
            if (options.html) {
                alertSpan.innerHTML = options.html;
            }
            alertWindow.classList.remove('hidden');
            var timeOutInSeconds = options.timeOut ?? 3000;
            if (options.autoHide) {
                setTimeout((elem) => {
                    elem.classList.add('hidden');
                }, timeOutInSeconds, alertWindow);
            }
        }
    }

};

class CustomerSelect {
    getCustomerUrl = '';
    fnCallback = null;
    btnCancel = null;
    btnSubmit = null;
    btnSearch = null;
    txtSearch = null;
    tbodyList = null;
    dialog = null;
    currentData = null;

    constructor(params) {
        this.getCustomerUrl = params.getCustomerUrl;
        this.fnCallback = params.selectCallback;
        this.#initialize();
    }

    show() {
        //search code
        if (this.btnSearch) {
            this.btnSearch.addEventListener("click", (evt) => {
                evt.preventDefault();
                this.#seach();
            }, { once: true });
        }

            //submit code
        if (this.btnSubmit) {
            this.btnSubmit.addEventListener("click", (evt) => {
                evt.preventDefault();

                this.#select();

                this.dialog.close();

            }, { once: true });
        }

        //cancel code
        if (this.btnCancel) {
            this.btnCancel.addEventListener("click", (evt) => {
                evt.preventDefault();
                this.dialog.close();
            }, { once: true });
        }

        if (this.dialog) this.dialog.showModal();
    }

    #initialize() {
        var dialogs = document.getElementsByClassName("customer-select-dialog");
        var cancelButtons = document.getElementsByClassName("customer-select-cancel");
        var submitButtons = document.getElementsByClassName("customer-select-submit");
        var searchButtons = document.getElementsByClassName("customer-select-search");
        var inputs = document.getElementsByClassName("customer-select-customer-name");
        var tbody = document.querySelector(".customer-select-list tbody");

        if (dialogs) this.dialog = dialogs[0];
        if (cancelButtons) this.btnCancel = cancelButtons[0];
        if (submitButtons) this.btnSubmit = submitButtons[0];
        if (searchButtons) this.btnSearch = searchButtons[0];
        if (inputs) this.txtSearch = inputs[0];
        if (tbody) this.tbodyList = tbody;
    }

    #seach = async () => {
        if (this.txtSearch) {
            //show progress indicator
            this.tbodyList.replaceChildren();
            const tempTr = document.createElement("tr");
            tempTr.innerHTML =
                `<td colspan="5" class="w-full h-32 text-center align-middle">
                    <div class="place-items-center">
                       <svg class="w-12 h-12 text-gray-300 animate-spin" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg" width="24" height="24">
                          <path
                            d="M32 3C35.8083 3 39.5794 3.75011 43.0978 5.20749C46.6163 6.66488 49.8132 8.80101 52.5061 11.4939C55.199 14.1868 57.3351 17.3837 58.7925 20.9022C60.2499 24.4206 61 28.1917 61 32C61 35.8083 60.2499 39.5794 58.7925 43.0978C57.3351 46.6163 55.199 49.8132 52.5061 52.5061C49.8132 55.199 46.6163 57.3351 43.0978 58.7925C39.5794 60.2499 35.8083 61 32 61C28.1917 61 24.4206 60.2499 20.9022 58.7925C17.3837 57.3351 14.1868 55.199 11.4939 52.5061C8.801 49.8132 6.66487 46.6163 5.20749 43.0978C3.7501 39.5794 3 35.8083 3 32C3 28.1917 3.75011 24.4206 5.2075 20.9022C6.66489 17.3837 8.80101 14.1868 11.4939 11.4939C14.1868 8.80099 17.3838 6.66487 20.9022 5.20749C24.4206 3.7501 28.1917 3 32 3L32 3Z"
                            stroke="currentColor" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"></path>
                          <path
                            d="M32 3C36.5778 3 41.0906 4.08374 45.1692 6.16256C49.2477 8.24138 52.7762 11.2562 55.466 14.9605C58.1558 18.6647 59.9304 22.9531 60.6448 27.4748C61.3591 31.9965 60.9928 36.6232 59.5759 40.9762"
                            stroke="currentColor" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" class="text-gray-900">
                          </path>
                       </svg>
                    </div>
                </td>`;
            this.tbodyList.appendChild(tempTr);
            
            var searchString = encodeURIComponent(this.txtSearch.value);
            var searchUrl = this.getCustomerUrl + '?searchString=' + searchString;

            var client = new WebClient(searchUrl, null);
            var result = await client.getAsync();
            if (result) {
                if (this.tbodyList) {
                    this.tbodyList.replaceChildren();

                    result.forEach((customer) => {
                        var tr = document.createElement("tr");
                        tr.classList.add("bg-white", "border-b", "hover:bg-gray-50", "cursor-pointer");
                        tr.innerHTML = `
                        <td class="px-6 py-4 text-center">
                            <input type="radio" name="customerSelect" value="${customer.id}">
                        </td>
                        <td class="px-6 py-4">${customer.firstname} ${customer.lastname}</td>
                        <td class="px-6 py-4">${customer.email}</td>
                        <td class="px-6 py-4">${customer.phoneNumber}</td>
                        <td class="px-6 py-4 text-center">
                            ${customer.isActive ? '<i class="fa-solid fa-check text-green-500"></i>' : '<i class="fa-solid fa-xmark text-red-500"></i>'}
                        </td>
                        `;
                        this.tbodyList.appendChild(tr);

                        tr.addEventListener('click', (evt) => {
                            evt.stopPropagation();
                            let selectInput = tr.querySelector('input');
                            if (selectInput) {
                                selectInput.checked = true;

                                selectInput.dispatchEvent(new Event('change', { bubbles: true }));
                            }
                        });

                        var selectInput = tr.querySelector('input');
                        if (selectInput) {
                            selectInput.addEventListener('change', (evt) => {
                                if (this.currentData == null) {
                                    this.btnSubmit.toggleAttribute('disabled');
                                    this.btnSubmit.classList.toggle('cursor-not-allowed');
                                    this.btnSubmit.classList.toggle('bg-gray-700');
                                    this.btnSubmit.classList.toggle('hover:bg-gray-500');
                                    this.btnSubmit.classList.toggle('bg-green-700');
                                    this.btnSubmit.classList.toggle('bg-green-500');
                                }
                                if (evt.target.checked) this.currentData = customer;
                            });
                        }
                    });
                }
            }
        }
    }

    #select() {
        this.fnCallback(this.currentData);
    }
};

class AccountSelect{
    //getAccountUrl = '';
    addUrl = '';
    fnCallback = null;
    btnCancel = null;
    btnSubmit = null;
    btnAdd = null;
    txtAdd = null;
    tbody = null;
    dialog = null;
    customerId = 0;
    constructor(params) {
        if (params) {
            if (!params.customerid || params.customerid <= 0) throw new Error("CustomerId is required to initialize AccountSelect");
            if (params.addAccountUrl) this.addUrl = params.addAccountUrl;
            if (params.submitCallback) this.fnCallback = params.submitCallback;
            //if (params.getAccountUrl) this.getAccountUrl = params.getAccountUrl;
            this.customerId = params.customerid;
        }
        this.#initialize();
    }

    show() {
        if (this.dialog) {
            this.dialog.showModal();
            this.#setAddButtonListener();
            this.#setSubmitButtonListener();
            this.#setCancelButtonListener();
        }
    }

    #initialize(){
        const elDialogs = document.getElementsByClassName("account-select-dialog");
        const elInputs = document.getElementsByClassName("account-select-account-address");
        const elAddButtons = document.getElementsByClassName("account-select-add");
        const elTableBody = document.querySelector(".account-select-list tbody");
        const elSubmitButtons = document.getElementsByClassName("account-select-submit");
        const elCancelButtons = document.getElementsByClassName("account-select-cancel");

        if (elDialogs) this.dialog = elDialogs[0];
        if (elInputs) this.txtAdd = elInputs[0];
        if (elAddButtons) this.btnAdd = elAddButtons[0];
        if (elTableBody) this.tbody = elTableBody;
        if (elSubmitButtons) this.btnSubmit = elSubmitButtons[0];
        if (elCancelButtons) this.btnCancel = elCancelButtons[0];
    }

    #setAddButtonListener() {
        if (this.btnAdd && this.addUrl != '') {
            if (!this.txtAdd) throw new Error('Text input for adding account is required');
            this.btnAdd.addEventListener("click", async (evt) => {
                evt.preventDefault();
                var accountAddress = encodeURIComponent(this.txtAdd.value.trim());
                if (this.txtAdd.value.trim() == '') throw new Error('Account address is required');
                var addClient = new WebClient(this.addUrl, { customerId: this.customerId, accountAddress: accountAddress });
                var account = await addClient.postAsync();
                if (account) {
                    this.#refreshTable(account);
                }
                else {
                    console.log('Account was not added');
                }

            }, { once: true });
        }
    }

    #setSubmitButtonListener() {
        if (this.btnSubmit && this.dialog) {
            this.btnSubmit.addEventListener("click", (evt) => {
                evt.preventDefault();
                var selectedRadio = this.dialog.querySelectorAll('input[type="radio"]:checked');
                if (selectedRadio) {
                    var accountId = selectedRadio[0].dataset.id;
                    var accountNumber = encodeURIComponent(selectedRadio[0].dataset.accountno);
                    var address = selectedRadio[0].dataset.address;
                    this.fnCallback(accountId, accountNumber, address);
                    this.dialog.close();
                }
            }, { once: true });
        }
    }

    #setCancelButtonListener() {
        if (this.btnCancel) {
            if (!this.fnCallback) throw new Error('Submit callback is required for Account Select');
            this.btnCancel.addEventListener("click", (evt) => {
                evt.preventDefault();
                
                if (this.dialog) this.dialog.close();
            }, { once: true });
        }
    }

    #refreshTable(account) {
        if (this.tbody) {
            //this.tbody.replaceChildren();

            var tr = document.createElement("tr");
            tr.classList.add("bg-white", "border-b", "hover:bg-gray-50");
            //<td class="px-6 py-4 text-center">${this.#loadAccountStatusContent(+ account.status)}</td>
            tr.innerHTML = `
                    <td class="px-6 py-4 text-center">
                        <input data-id="{account.id}" data-accountno="${account.accountNumber}" data-address="${encodeURIComponent(account.address)}" data-current="${account.isCurrentAddress}" type="radio" name="customerSelect" value="${account.id}">
                    </td>
                    <td class="px-6 py-4">${account.accountNumber}</td>
                    <td class="px-6 py-4">${account.address}</td>
                    <td class="px-6 py-4">
                        ${account.isCurrentAddress ? '<i class="fa-solid fa-check text-green-500"></i>' : ''}
                    </td>
                `;
            tr.appendChild(this.#addAccountStatusColumn(account.status));
            this.tbody.appendChild(tr);
        }
    }

    #addAccountStatusColumn(status) {
        var icon = document.createElement("i");
        var td = document.createElement("td");
        td.classList.add("px-6", "py-4");
        switch (status) {
            case 1:
                break;
                icon.classList.add("fa-solid", "fa-question", "text-yellow-500");
            case 2:
                icon.classList.add("fa-solid", "fa-pause", "text-gray-500");
                break;
            case 3:
                icon.classList.add("fa-solid", "fa-check", "text-green-500");
                break;
            default:
                icon.classList.add("fa-solid", "fa-xmark", "text-red-500");
                break;
        }
        td.appendChild(icon);
        return td;
    }

};