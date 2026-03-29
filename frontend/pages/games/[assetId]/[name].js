import { useRouter } from 'next/router';
import SharedAssetPage from "../../../components/sharedAssetPage2019";
import { getProductInfoLegacy } from '../../../services/catalog';
import Head from 'next/head';
import Theme2016 from '../../../components/theme2016';

const GamePage = ({ name, description, assetId, ...props }) => {
  return (
    <>
      {name && (
        <Head>
          <title>{name} - Marine</title>
          <meta property="og:title" content={name} />
          <meta property="og:url" content={`https://silrev.biz/games/${assetId}/--`} />
          <meta property="og:type" content="profile" />
          <meta property="og:description" content={description} />
          <meta property="og:image" content={`https://silrev.biz/thumbs/asset.ashx?assetId=${assetId}`} />
          <meta name="twitter:card" content="summary_large_image" />
          <meta name="og:site_name" content="Marine" />
          <meta name="theme-color" content="#E2231A" />
        </Head>
      )}
      <Theme2016>
        <SharedAssetPage idParamName='assetId' nameParamName='name' />
      </Theme2016>
    </>
  );
}
export async function getServerSideProps(context) {
  const { assetId } = context.query;
  const info = await getProductInfoLegacy(assetId);
  try {
    if (info.AssetTypeId != 9) {
      return {
        props: {
          name: null,
          description: null,
          assetId: assetId
        }
      }
    } else {
      return {
        props: {
          name: info.Name,
          description: info.Description,
          assetId: assetId
        }
      }
    }
  } catch (error) {
    console.error(error);
    return {
      props: {
        name: null,
        description: null,
        assetId: null
      }
    };
  }
}
export default GamePage;
