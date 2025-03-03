﻿using Sortly.Api.Common.Enums;
using Sortly.Api.Common.Exceptions;
using Sortly.Api.Model.Abstractions;
using Sortly.Api.Model.Sortly;
using System.IO.Abstractions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sortly.Api.Model.Request
{
    public class CreateItemGroupRequest : IValidatable, IPhotoRequest
    {
        [JsonPropertyName("item_group")]
        public SubCreateItemGroupRequest ItemGroup { get; set; }

        public void Validate()
        {
            if (ItemGroup == null)
                throw new SortlyValidationException("ItemGroup must be defined");

            ItemGroup.Validate();
        }

        public HttpContent AsHttpPayload(IFileSystem fileSystem)
        {
            if (!ItemGroup.Photos?.Any(x => x.PhotoRequestType == PhotoRequestType.Path) ?? true)
                return new StringContent(JsonSerializer.Serialize(this), Encoding.UTF8, "application/json");

            return ItemGroup.AsMultiPartForm(fileSystem);
        }
    }

    // NOTE: for some reason the create/update payload differ. This is needed to nest this info into "item_group"
    public class SubCreateItemGroupRequest: IValidatable
    {
        private const string JSON_ITEM_GROUP = "item_group";
        private const string JSON_NAME = "name";
        private const string JSON_LABEL_URL = "label_url";
        private const string JSON_LABEL_URL_TYPE = "label_url_type";
        private const string JSON_LABEL_URL_EXTRA = "label_url_extra";
        private const string JSON_LABEL_URL_EXTRA_TYPE = "label_url_extra_type";
        private const string JSON_GROUP_ATTRIBUTES = "group_attributes";
        private const string JSON_PHOTOS = "photos";
        private const string JSON_PHOTO_IDS = "photo_ids";
        private const string JSON_SORTLY_ID = "sid";

        [JsonPropertyName(JSON_NAME)]
        public string Name { get; set; }

        [JsonPropertyName(JSON_SORTLY_ID)]
        public string SortlyId { get; set; }

        [JsonPropertyName(JSON_LABEL_URL)]
        public string LabelUrl { get; set; }

        [JsonPropertyName(JSON_LABEL_URL_TYPE)]
        public string LabelUrlType { get; set; }

        [JsonPropertyName(JSON_LABEL_URL_EXTRA)]
        public string LabelUrlExtra { get; set; }

        [JsonPropertyName(JSON_LABEL_URL_EXTRA_TYPE)]
        public string LabelUrlExtraType { get; set; }

        [JsonPropertyName(JSON_PHOTO_IDS)]
        public int[] PhotoIds { get; set; }

        [JsonPropertyName(JSON_PHOTOS)]
        public PhotoRequest[] Photos { get; set; }

        [JsonPropertyName(JSON_GROUP_ATTRIBUTES)]
        public ItemGroupAttribute[] Attributes { get; set; }

        private void AddStringContent(MultipartFormDataContent form, string name, object value)
        {
            if (value != null)
                form.Add(new StringContent(value.ToString(), Encoding.UTF8), name);
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(Name))
                throw new SortlyValidationException("Name is required");

            if ((Attributes?.Length ?? 0) == 0)
                throw new SortlyValidationException("At least one attribute is required");

            for (int i = 0; i < Attributes.Length; i++)
            {
                var attribute = Attributes[i];

                if (string.IsNullOrEmpty(attribute.Name))
                    throw new SortlyValidationException($"Group attribute at index {i} - Name is required.");

                if (attribute.Order == null)
                    throw new SortlyValidationException($"Group attribute at index {i} - Order is required.");

                if ((attribute.Options?.Length ?? 0) == 0)
                    throw new SortlyValidationException($"Group attribute at index {i} - At least one option is required.");

                for (int j = 0; j < attribute.Options.Length; j++)
                {
                    var option = attribute.Options[j];

                    if (string.IsNullOrEmpty(option.Name))
                        throw new SortlyValidationException($"Group attribute at index {i}, Option at index {j} - Name is required.");

                    if (option.Order == null)
                        throw new SortlyValidationException($"Group attribute at index {i}, Option at index {j} - Order is required.");
                }
            }
        }

        public HttpContent AsMultiPartForm(IFileSystem _fileSystem)
        {
            var form = new MultipartFormDataContent();

            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_NAME}]", Name);
            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_LABEL_URL}]", LabelUrl);
            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_LABEL_URL_TYPE}]", LabelUrlType);
            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_LABEL_URL_EXTRA}]", LabelUrlExtra);
            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_LABEL_URL_EXTRA_TYPE}]", LabelUrlExtraType);
            AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_LABEL_URL_EXTRA_TYPE}]", LabelUrlExtraType);

            foreach (var t in (Attributes ?? Enumerable.Empty<ItemGroupAttribute>()))
            {
                AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_GROUP_ATTRIBUTES}][][name]", t.Name);
                AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_GROUP_ATTRIBUTES}][][order]", t.Order);

                foreach (var o in (t.Options ?? Enumerable.Empty<ItemGroupAttributeOption>()))
                {
                    AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_GROUP_ATTRIBUTES}][][options][][name]", o.Name);
                    AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_GROUP_ATTRIBUTES}][][options][][order]", o.Order);
                }
            }

            foreach (var t in (PhotoIds ?? Enumerable.Empty<int>()))
                AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_PHOTO_IDS}][]", t.ToString());

            foreach (var p in (Photos ?? Enumerable.Empty<PhotoRequest>()))
            {
                if (p.PhotoRequestType == PhotoRequestType.Path)
                {
                    var file = _fileSystem.File.ReadAllBytes(p.Content);

                    form.Add(new ByteArrayContent(file, 0, file.Length), $"{JSON_ITEM_GROUP}[{JSON_PHOTOS}][]", Path.GetFileName(p.Content));
                }
                else if (p.PhotoRequestType.HasFlag(PhotoRequestType.Url | PhotoRequestType.Blob))
                    AddStringContent(form, $"{JSON_ITEM_GROUP}[{JSON_PHOTOS}][]", p.Content);
            }

            return form;
        }
    }
}
