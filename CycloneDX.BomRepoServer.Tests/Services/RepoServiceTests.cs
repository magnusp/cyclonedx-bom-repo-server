using System;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using XFS = System.IO.Abstractions.TestingHelpers.MockUnixSupport;
using System.Threading.Tasks;
using CycloneDX.BomRepoServer.Controllers;
using CycloneDX.BomRepoServer.Exceptions;
using Xunit;
using CycloneDX.BomRepoServer.Options;
using CycloneDX.BomRepoServer.Services;
using CycloneDX.Models.v1_3;

namespace CycloneDX.BomRepoServer.Tests.Services
{
    public class BomServiceTests
    {
        [Theory]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b79", true)]
        [InlineData("urn:uuid:{3e671687-395b-41f5-a30f-a58921a69b79}", true)]
        [InlineData("urn_uuid_3e671687-395b-41f5-a30f-a58921a69b70", false)]
        [InlineData("urn:uuid:3e671687-395b-41f5-a30f-a58921a69b7", false)]
        [InlineData("abc", false)]
        public void ValidSerialNumberTest(string serialNumber, bool valid)
        {
            Assert.Equal(valid, BomController.ValidSerialNumber(serialNumber));            
        }
        
        [Fact]
        public async Task RetrieveAll_ReturnsAllVersions()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.Store(bom);
            bom.Version = 2;
            await service.Store(bom);
            bom.Version = 3;
            await service.Store(bom);

            var retrievedBoms = await service.RetrieveAll(bom.SerialNumber);
            
            Assert.Collection(retrievedBoms, 
                bom => Assert.Equal(1, bom.Version),
                bom => Assert.Equal(2, bom.Version),
                bom => Assert.Equal(3, bom.Version)
            );
        }
        
        [Fact]
        public async Task RetrieveLatest_ReturnsLatestVersion()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.Store(bom);
            bom.Version = 2;
            await service.Store(bom);
            bom.Version = 3;
            await service.Store(bom);

            var retrievedBom = await service.RetrieveLatest(bom.SerialNumber);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Fact]
        public async Task StoreBom_StoresSpecificVersion()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };

            await service.Store(bom);

            var retrievedBom = await service.Retrieve(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(retrievedBom.SerialNumber, bom.SerialNumber);
            Assert.Equal(retrievedBom.Version, bom.Version);
        }
        
        [Fact]
        public async Task StoreClashingBomVersion_ThrowsException()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };

            await service.Store(bom);

            await Assert.ThrowsAsync<BomAlreadyExistsException>(async () => await service.Store(bom));
        }
        
        [Fact]
        public async Task StoreBomWithoutVersion_SetsVersion()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid()
            };

            var returnedBom = await service.Store(bom);

            var retrievedBom = await service.Retrieve(bom.SerialNumber, bom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(1, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public async Task StoreBomWithPreviousVersions_IncrementsFromPreviousVersion()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 2,
            };
            // store previous version
            await service.Store(bom);

            // store new version without a version number
            bom.Version = null;
            var returnedBom = await service.Store(bom);

            var retrievedBom = await service.Retrieve(returnedBom.SerialNumber, returnedBom.Version.Value);
            
            Assert.Equal(bom.SerialNumber, returnedBom.SerialNumber);
            Assert.Equal(3, returnedBom.Version);
            Assert.Equal(returnedBom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(returnedBom.Version, retrievedBom.Version);
        }
        
        [Fact]
        public async Task Delete_DeletesSpecificVersion()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.Store(bom);
            bom.Version = 2;
            await service.Store(bom);
            
            service.Delete(bom.SerialNumber, bom.Version.Value);

            var retrievedBom = await service.Retrieve(bom.SerialNumber, bom.Version.Value);
            Assert.Null(retrievedBom);
            retrievedBom = await service.Retrieve(bom.SerialNumber, 1);
            Assert.Equal(bom.SerialNumber, retrievedBom.SerialNumber);
            Assert.Equal(1, retrievedBom.Version);
        }
        
        [Fact]
        public async Task DeleteAll_DeletesAllVersions()
        {
            var mfs = new MockFileSystem();
            var options = new RepoOptions
            {
                Directory = "repo"
            };
            var service = new RepoService(mfs, options);
            var bom = new Bom
            {
                SerialNumber = "urn:uuid:" + Guid.NewGuid(),
                Version = 1,
            };
            await service.Store(bom);
            bom.Version = 2;
            await service.Store(bom);
            
            service.DeleteAll(bom.SerialNumber);

            var retrievedBom = await service.Retrieve(bom.SerialNumber, 1);
            Assert.Null(retrievedBom);
            retrievedBom = await service.Retrieve(bom.SerialNumber, 2);
            Assert.Null(retrievedBom);
        }
    }
}