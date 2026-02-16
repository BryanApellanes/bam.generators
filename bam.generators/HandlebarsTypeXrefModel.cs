using Bam.Data.Repositories;

namespace Bam.Generators
{
    /// <summary>
    /// Extends <see cref="TypeXrefModel"/> with Handlebars template-friendly properties for cross-reference
    /// (many-to-many) relationships, providing pluralized and camelCase name variants.
    /// </summary>
    public class HandlebarsTypeXrefModel : TypeXrefModel
    {
        /// <summary>
        /// Gets the fully qualified cross-reference table name in the format "{DaoNamespace}.{LeftName}{RightName}".
        /// </summary>
        public string XrefTableName
        {
            get
            {
                return string.Format("{0}.{1}{2}", DaoNamespace, Left.Name, Right.Name);
            }
        }

        /// <summary>
        /// Gets the pluralized right type name in camelCase format.
        /// </summary>
        public string CamelCasedRightNamePluralized
        {
            get
            {
                return RightNamePluralized.CamelCase();
            }
        }

        /// <summary>
        /// Gets the pluralized form of the right type name.
        /// </summary>
        public string RightNamePluralized
        {
            get
            {
                return Right.Name.Pluralize();
            }
        }

        /// <summary>
        /// Gets the pluralized form of the right DAO type name.
        /// </summary>
        public string RightDaoNamePluralized
        {
            get
            {
                return RightDaoName.Pluralize();
            }
        }

        /// <summary>
        /// Gets the pluralized left type name in camelCase format.
        /// </summary>
        public string CamelCasedLeftNamePluralized
        {
            get
            {
                return LeftNamePluralized.CamelCase();
            }
        }

        /// <summary>
        /// Gets the pluralized form of the left type name.
        /// </summary>
        public string LeftNamePluralized
        {
            get
            {
                return Left.Name.Pluralize();
            }
        }

        /// <summary>
        /// Gets the pluralized form of the left DAO type name.
        /// </summary>
        public string LeftDaoNamePluralized
        {
            get
            {
                return LeftDaoName.Pluralize();
            }
        }
    }
}
