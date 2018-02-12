namespace App1
{
    using System;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.Storage.Pickers;
    using Windows.Storage.Streams;

    static class FileDialogExtensions
    {
        public static async Task<StorageFile> PickFileForReadAsync(
          string fileExtension)
        {
            IRandomAccessStream stream = null;
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(fileExtension);

            var file = await picker.PickSingleFileAsync();

            return (file);
        }
        public static async Task<StorageFile> PickFileForSaveAsync(
          string typeOfFile, string typeOfFileExtension, string suggestedName)
        {
            IOutputStream stream = null;
            var picker = new FileSavePicker();

            picker.FileTypeChoices.Add(
              typeOfFile, new string[] { typeOfFileExtension });

            picker.SuggestedFileName = suggestedName;
            picker.SuggestedStartLocation = PickerLocationId.Desktop;

            var file = await picker.PickSaveFileAsync();

            return (file);
        }
    }
}
