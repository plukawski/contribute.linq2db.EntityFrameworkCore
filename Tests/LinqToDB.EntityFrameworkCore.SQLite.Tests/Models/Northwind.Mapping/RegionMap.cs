﻿using LinqToDB.EntityFrameworkCore.BaseTests.Models.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinqToDB.EntityFrameworkCore.SQLite.Tests.Models.Northwind.Mapping
{
	public class RegionMap : IEntityTypeConfiguration<Region>
	{
		public void Configure(EntityTypeBuilder<Region> builder)
		{
			builder.HasKey(e => e.RegionId);

			builder.Property(e => e.RegionId)
				.HasColumnName("RegionID")
				.ValueGeneratedNever();

			builder.Property(e => e.RegionDescription)
				.IsRequired()
				.HasMaxLength(50);
		}
	}
}
