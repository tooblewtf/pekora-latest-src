import Games from "../../components/gamesPage"
import Theme2016 from "../../components/theme2016";
import GamesPageStore from "../../stores/gamesPage";

const GamesPage = props => {
  return <Theme2016>
    <GamesPageStore.Provider>
      <Games />
    </GamesPageStore.Provider>
  </Theme2016>
}

export const getStaticProps = () => {
  return {
    props: {
      title: 'Games - Marine',
    }
  }
}

export default GamesPage;