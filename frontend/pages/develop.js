import { useRouter } from "next/dist/client/router";
import Develop from "../components/develop";
import t from "../lib/t";
import LibraryStore from "../stores/libraryPage";

const DevelopPage = props => {
  const router = useRouter();
  const id = t.string(router.query['View']);

  return <LibraryStore.Provider>
    <Develop id={parseInt(id, 10) || 0} />
  </LibraryStore.Provider>
}

DevelopPage.getInitialProps = () => {
  return {
    title: 'Develop - Marine',
  }
}

export default DevelopPage;
